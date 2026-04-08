using CoupleFinance.Domain.Common;
using CoupleFinance.Domain.Entities;
using CoupleFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CoupleFinance.Infrastructure.Services;

public sealed partial class FinanceWorkspaceService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static async Task ApplyTransactionImpactAsync(AppDbContext db, Transaction entity, CancellationToken cancellationToken)
    {
        if (!entity.BankAccountId.HasValue)
        {
            return;
        }

        var account = await db.BankAccounts.FirstOrDefaultAsync(x => x.Id == entity.BankAccountId.Value, cancellationToken);
        if (account is null)
        {
            return;
        }

        account.CurrentBalance += entity.GetSignedAmount();
    }

    private static async Task RevertTransactionImpactAsync(AppDbContext db, Transaction entity, CancellationToken cancellationToken)
    {
        if (!entity.BankAccountId.HasValue)
        {
            return;
        }

        var account = await db.BankAccounts.FirstOrDefaultAsync(x => x.Id == entity.BankAccountId.Value, cancellationToken);
        if (account is null)
        {
            return;
        }

        account.CurrentBalance -= entity.GetSignedAmount();
    }

    private static async Task<Invoice> GetOrCreateInvoiceAsync(AppDbContext db, Guid householdId, CreditCard card, int referenceMonth, int referenceYear, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(
            x => x.CreditCardId == card.Id && x.ReferenceMonth == referenceMonth && x.ReferenceYear == referenceYear,
            cancellationToken);

        if (invoice is not null)
        {
            return invoice;
        }

        invoice = new Invoice
        {
            HouseholdId = householdId,
            CreditCardId = card.Id,
            ReferenceMonth = referenceMonth,
            ReferenceYear = referenceYear,
            ClosingDate = new DateTime(referenceYear, referenceMonth, Math.Min(card.ClosingDay, DateTime.DaysInMonth(referenceYear, referenceMonth))),
            DueDate = new DateTime(referenceYear, referenceMonth, Math.Min(card.DueDay, DateTime.DaysInMonth(referenceYear, referenceMonth))),
            TotalAmount = 0
        };

        db.Invoices.Add(invoice);
        return invoice;
    }

    private static async Task UpsertAndQueueAsync<TEntity>(AppDbContext db, TEntity entity, CancellationToken cancellationToken)
        where TEntity : SyncEntity
    {
        entity.Touch();
        var existing = await db.Set<TEntity>().FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        if (existing is null)
        {
            db.Set<TEntity>().Add(entity);
        }
        else if (!ReferenceEquals(existing, entity))
        {
            db.Entry(existing).CurrentValues.SetValues(entity);
        }

        await QueueEntityAsync(db, entity, cancellationToken);
    }

    private static async Task QueueEntityAsync<TEntity>(AppDbContext db, TEntity entity, CancellationToken cancellationToken)
        where TEntity : SyncEntity
    {
        entity.Touch();
        var entityName = typeof(TEntity).Name;
        var payload = JsonSerializer.Serialize(entity, SerializerOptions);
        var queueItem = await db.SyncQueueItems.FirstOrDefaultAsync(x => x.EntityName == entityName && x.EntityId == entity.Id, cancellationToken);
        if (queueItem is null)
        {
            queueItem = new SyncQueueItem
            {
                HouseholdId = entity.HouseholdId,
                EntityName = entityName,
                EntityId = entity.Id,
                PayloadJson = payload
            };
            db.SyncQueueItems.Add(queueItem);
        }
        else
        {
            queueItem.PayloadJson = payload;
            queueItem.Attempts = 0;
            queueItem.LastError = null;
        }
    }
}
