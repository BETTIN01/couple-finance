using CoupleFinance.Application.Contracts;
using CoupleFinance.Application.Models.Auth;
using CoupleFinance.Application.Models.Sync;
using CoupleFinance.Domain.Common;
using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;
using CoupleFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace CoupleFinance.Infrastructure.Services;

public sealed class SyncCoordinator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    SupabaseRestClient supabaseClient,
    ILogger<SyncCoordinator> logger) : ISyncCoordinator
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<SyncResult> SyncAsync(AuthSession session, bool force = false, CancellationToken cancellationToken = default)
    {
        if (!supabaseClient.IsConfigured)
        {
            return new SyncResult(true, 0, 0, DateTime.UtcNow, null);
        }

        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return new SyncResult(false, 0, 0, DateTime.UtcNow, "Sem conexão com a internet.");
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await db.Database.EnsureCreatedAsync(cancellationToken);

            var uploaded = 0;
            var downloaded = 0;
            var queueItems = await db.SyncQueueItems
                .Where(x => x.HouseholdId == session.HouseholdId)
                .OrderBy(x => x.CreatedAtUtc)
                .Take(force ? 500 : 120)
                .ToListAsync(cancellationToken);

            foreach (var queueItem in queueItems)
            {
                try
                {
                    DateTime? deletedAt = queueItem.Operation == SyncOperation.Delete ? DateTime.UtcNow : null;
                    await supabaseClient.UpsertSyncDocumentAsync(
                        new RemoteSyncDocument(
                            queueItem.HouseholdId,
                            queueItem.EntityName,
                            queueItem.EntityId,
                            JsonDocument.Parse(queueItem.PayloadJson).RootElement.Clone(),
                            DateTime.UtcNow,
                            deletedAt),
                        session.AccessToken,
                        cancellationToken);

                    db.SyncQueueItems.Remove(queueItem);
                    uploaded++;
                }
                catch (Exception ex)
                {
                    queueItem.Attempts++;
                    queueItem.LastAttemptAtUtc = DateTime.UtcNow;
                    queueItem.LastError = ex.Message;
                    logger.LogWarning(ex, "Falha ao enviar item {EntityName}/{EntityId}", queueItem.EntityName, queueItem.EntityId);
                }
            }

            var household = await db.Households.FirstAsync(x => x.Id == session.HouseholdId, cancellationToken);
            var remoteDocuments = await supabaseClient.GetChangedDocumentsAsync(session.HouseholdId, household.LastSyncedAtUtc, session.AccessToken, cancellationToken);
            foreach (var document in remoteDocuments)
            {
                if (await ApplyRemoteDocumentAsync(db, document, cancellationToken))
                {
                    downloaded++;
                }
            }

            household.LastSyncedAtUtc = DateTime.UtcNow;
            household.SyncStatus = SyncStatus.Synced;
            await db.SaveChangesAsync(cancellationToken);

            return new SyncResult(true, uploaded, downloaded, DateTime.UtcNow, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sincronização falhou");
            return new SyncResult(false, 0, 0, DateTime.UtcNow, ex.Message);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<SyncHealth> GetHealthAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var household = await db.Households.FirstOrDefaultAsync(x => x.Id == householdId, cancellationToken);
        var pendingItems = await db.SyncQueueItems.CountAsync(x => x.HouseholdId == householdId, cancellationToken);
        var statusText = !supabaseClient.IsConfigured
            ? "Modo local"
            : pendingItems == 0
                ? "Sincronizado"
                : $"{pendingItems} atualização(ões) pendente(s)";

        return new SyncHealth(
            supabaseClient.IsConfigured,
            NetworkInterface.GetIsNetworkAvailable(),
            pendingItems,
            household?.LastSyncedAtUtc,
            statusText,
            null);
    }

    private static async Task<bool> ApplyRemoteDocumentAsync(AppDbContext db, RemoteSyncDocument document, CancellationToken cancellationToken)
    {
        return document.EntityName switch
        {
            nameof(UserProfile) => await UpsertAsync<UserProfile>(db, document, cancellationToken),
            nameof(Household) => await UpsertAsync<Household>(db, document, cancellationToken),
            nameof(HouseholdMember) => await UpsertAsync<HouseholdMember>(db, document, cancellationToken),
            nameof(Category) => await UpsertAsync<Category>(db, document, cancellationToken),
            nameof(BankAccount) => await UpsertAsync<BankAccount>(db, document, cancellationToken),
            nameof(Transaction) => await UpsertAsync<Transaction>(db, document, cancellationToken),
            nameof(Transfer) => await UpsertAsync<Transfer>(db, document, cancellationToken),
            nameof(CreditCard) => await UpsertAsync<CreditCard>(db, document, cancellationToken),
            nameof(CardPurchase) => await UpsertAsync<CardPurchase>(db, document, cancellationToken),
            nameof(Installment) => await UpsertAsync<Installment>(db, document, cancellationToken),
            nameof(Invoice) => await UpsertAsync<Invoice>(db, document, cancellationToken),
            nameof(Goal) => await UpsertAsync<Goal>(db, document, cancellationToken),
            nameof(InvestmentAsset) => await UpsertAsync<InvestmentAsset>(db, document, cancellationToken),
            nameof(Insight) => await UpsertAsync<Insight>(db, document, cancellationToken),
            _ => false
        };
    }

    private static async Task<bool> UpsertAsync<TEntity>(AppDbContext db, RemoteSyncDocument document, CancellationToken cancellationToken)
        where TEntity : SyncEntity
    {
        var entity = JsonSerializer.Deserialize<TEntity>(document.PayloadJson, SerializerOptions);
        if (entity is null)
        {
            return false;
        }

        var existing = await db.Set<TEntity>().FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        if (existing is null)
        {
            entity.SyncStatus = SyncStatus.Synced;
            entity.LastSyncedAtUtc = DateTime.UtcNow;
            db.Set<TEntity>().Add(entity);
            return true;
        }

        if (existing.UpdatedAtUtc > entity.UpdatedAtUtc)
        {
            return false;
        }

        db.Entry(existing).CurrentValues.SetValues(entity);
        existing.SyncStatus = SyncStatus.Synced;
        existing.LastSyncedAtUtc = DateTime.UtcNow;
        return true;
    }
}
