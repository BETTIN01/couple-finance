using CoupleFinance.Application.Contracts;
using CoupleFinance.Application.Models;
using CoupleFinance.Application.Models.Auth;
using CoupleFinance.Application.Models.Dashboard;
using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;
using CoupleFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoupleFinance.Infrastructure.Services;

public sealed partial class FinanceWorkspaceService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IDashboardComposer dashboardComposer,
    IProjectionService projectionService,
    IInsightEngine insightEngine,
    ISyncCoordinator syncCoordinator,
    ILogger<FinanceWorkspaceService> logger) : IFinanceWorkspaceService
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task InitializeAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await db.Categories.AnyAsync(x => x.HouseholdId == session.HouseholdId, cancellationToken))
        {
            db.Categories.AddRange(SeedDataFactory.DefaultCategories(session.HouseholdId));
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.Insights.AnyAsync(x => x.HouseholdId == session.HouseholdId, cancellationToken))
        {
            await RefreshInsightsAsync(session, DateTime.Today, cancellationToken);
        }
    }

    public async Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync(AuthSession session, PeriodFilter filter, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var currentUser = await db.UserProfiles.AsNoTracking().FirstAsync(x => x.Id == session.UserId, cancellationToken);
        var household = await db.Households.AsNoTracking().FirstAsync(x => x.Id == session.HouseholdId, cancellationToken);
        var partner = await db.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.HouseholdId == session.HouseholdId && x.Id != session.UserId, cancellationToken);

        var categories = await db.Categories.AsNoTracking().Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var accounts = await db.BankAccounts.AsNoTracking().Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var transactions = await db.Transactions.AsNoTracking().Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.UpdatedAtUtc).ToListAsync(cancellationToken);
        var transfers = await db.Transfers.AsNoTracking().Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).OrderByDescending(x => x.OccurredOn).ToListAsync(cancellationToken);
        var creditCards = await db.CreditCards.AsNoTracking().Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var purchases = await db.CardPurchases.AsNoTracking().Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).OrderByDescending(x => x.PurchaseDate).ToListAsync(cancellationToken);
        var invoices = await db.Invoices.AsNoTracking().Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).OrderByDescending(x => x.ReferenceYear).ThenByDescending(x => x.ReferenceMonth).ToListAsync(cancellationToken);
        var goals = (await db.Goals.AsNoTracking().Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).ToListAsync(cancellationToken))
            .OrderByDescending(x => x.ProgressPercentage)
            .ToList();
        var investments = (await db.InvestmentAssets.AsNoTracking().Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).ToListAsync(cancellationToken))
            .OrderByDescending(x => x.CurrentValue)
            .ToList();
        var insights = await db.Insights.AsNoTracking().Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

        var dashboard = dashboardComposer.Build(transactions, categories, creditCards, invoices, investments, filter, session.UserId);
        var projection = projectionService.BuildSummary(transactions, goals, investments, filter);
        var sync = await syncCoordinator.GetHealthAsync(session.HouseholdId, cancellationToken);

        return new WorkspaceSnapshot(
            currentUser,
            household,
            partner,
            dashboard,
            projection,
            sync,
            household.InviteCode,
            categories,
            accounts,
            transactions,
            transfers,
            creditCards,
            purchases,
            invoices,
            goals,
            investments,
            insights);
    }

    public async Task SaveBankAccountAsync(AuthSession session, BankAccountInput input, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var entity = input.Id.HasValue
                ? await db.BankAccounts.FirstOrDefaultAsync(x => x.Id == input.Id.Value, cancellationToken) ?? new BankAccount { Id = input.Id.Value, HouseholdId = session.HouseholdId }
                : new BankAccount { HouseholdId = session.HouseholdId };

            entity.Name = input.Name.Trim();
            entity.Institution = input.Institution.Trim();
            entity.Type = input.Type;
            entity.CurrentBalance = input.CurrentBalance;
            entity.ColorHex = input.ColorHex;

            await UpsertAndQueueAsync(db, entity, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await SafeSyncAsync(session, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveTransactionAsync(AuthSession session, TransactionInput input, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            Transaction entity;

            if (input.Id.HasValue)
            {
                entity = await db.Transactions.FirstOrDefaultAsync(x => x.Id == input.Id.Value, cancellationToken)
                    ?? new Transaction { Id = input.Id.Value, HouseholdId = session.HouseholdId, OwnerUserId = session.UserId };
                await RevertTransactionImpactAsync(db, entity, cancellationToken);
            }
            else
            {
                entity = new Transaction
                {
                    HouseholdId = session.HouseholdId,
                    OwnerUserId = session.UserId
                };
            }

            entity.Description = input.Description.Trim();
            entity.Amount = Math.Abs(input.Amount);
            entity.OccurredOn = input.OccurredOn;
            entity.CategoryId = input.CategoryId;
            entity.BankAccountId = input.BankAccountId;
            entity.Kind = input.Kind;
            entity.Scope = input.Scope;
            entity.Notes = input.Notes;

            await ApplyTransactionImpactAsync(db, entity, cancellationToken);
            await UpsertAndQueueAsync(db, entity, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await SafeSyncAsync(session, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveTransferAsync(AuthSession session, TransferInput input, CancellationToken cancellationToken = default)
    {
        if (input.FromBankAccountId == input.ToBankAccountId)
        {
            throw new InvalidOperationException("A conta de origem deve ser diferente da conta de destino.");
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var fromAccount = await db.BankAccounts.FirstAsync(x => x.Id == input.FromBankAccountId, cancellationToken);
            var toAccount = await db.BankAccounts.FirstAsync(x => x.Id == input.ToBankAccountId, cancellationToken);

            var transfer = new Transfer
            {
                HouseholdId = session.HouseholdId,
                OwnerUserId = session.UserId,
                FromBankAccountId = input.FromBankAccountId,
                ToBankAccountId = input.ToBankAccountId,
                Amount = Math.Abs(input.Amount),
                OccurredOn = input.OccurredOn,
                Description = input.Description.Trim()
            };

            var debit = new Transaction
            {
                HouseholdId = session.HouseholdId,
                OwnerUserId = session.UserId,
                Description = $"Transferência para {toAccount.Name}",
                Amount = transfer.Amount,
                OccurredOn = input.OccurredOn,
                BankAccountId = fromAccount.Id,
                Kind = TransactionKind.TransferOut,
                Scope = EntryScope.Joint,
                TransferId = transfer.Id
            };

            var credit = new Transaction
            {
                HouseholdId = session.HouseholdId,
                OwnerUserId = session.UserId,
                Description = $"Transferência de {fromAccount.Name}",
                Amount = transfer.Amount,
                OccurredOn = input.OccurredOn,
                BankAccountId = toAccount.Id,
                Kind = TransactionKind.TransferIn,
                Scope = EntryScope.Joint,
                TransferId = transfer.Id
            };

            transfer.DebitTransactionId = debit.Id;
            transfer.CreditTransactionId = credit.Id;
            fromAccount.CurrentBalance -= transfer.Amount;
            toAccount.CurrentBalance += transfer.Amount;

            await UpsertAndQueueAsync(db, transfer, cancellationToken);
            await UpsertAndQueueAsync(db, debit, cancellationToken);
            await UpsertAndQueueAsync(db, credit, cancellationToken);
            await QueueEntityAsync(db, fromAccount, cancellationToken);
            await QueueEntityAsync(db, toAccount, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await SafeSyncAsync(session, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveCreditCardAsync(AuthSession session, CreditCardInput input, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var entity = input.Id.HasValue
                ? await db.CreditCards.FirstOrDefaultAsync(x => x.Id == input.Id.Value, cancellationToken) ?? new CreditCard { Id = input.Id.Value, HouseholdId = session.HouseholdId }
                : new CreditCard { HouseholdId = session.HouseholdId };

            entity.Name = input.Name.Trim();
            entity.Brand = input.Brand.Trim();
            entity.LimitAmount = input.LimitAmount;
            entity.ClosingDay = input.ClosingDay;
            entity.DueDay = input.DueDay;
            entity.ColorHex = input.ColorHex;

            await UpsertAndQueueAsync(db, entity, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await SafeSyncAsync(session, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveCardPurchaseAsync(AuthSession session, CardPurchaseInput input, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var card = await db.CreditCards.FirstAsync(x => x.Id == input.CreditCardId, cancellationToken);
            var purchase = new CardPurchase
            {
                HouseholdId = session.HouseholdId,
                CreditCardId = card.Id,
                CategoryId = input.CategoryId,
                OwnerUserId = session.UserId,
                Description = input.Description.Trim(),
                Amount = Math.Abs(input.Amount),
                PurchaseDate = input.PurchaseDate,
                InstallmentCount = Math.Max(1, input.InstallmentCount),
                Scope = input.Scope,
                Notes = input.Notes
            };

            await UpsertAndQueueAsync(db, purchase, cancellationToken);

            var baseInvoiceDate = input.PurchaseDate.Day > card.ClosingDay ? input.PurchaseDate.AddMonths(1) : input.PurchaseDate;
            var baseInstallmentValue = Math.Round(purchase.Amount / purchase.InstallmentCount, 2, MidpointRounding.AwayFromZero);
            var remaining = purchase.Amount;

            for (var installmentNumber = 1; installmentNumber <= purchase.InstallmentCount; installmentNumber++)
            {
                var invoiceDate = baseInvoiceDate.AddMonths(installmentNumber - 1);
                var invoice = await GetOrCreateInvoiceAsync(db, session.HouseholdId, card, invoiceDate.Month, invoiceDate.Year, cancellationToken);
                var amount = installmentNumber == purchase.InstallmentCount ? remaining : baseInstallmentValue;
                remaining -= amount;

                invoice.TotalAmount += amount;
                await UpsertAndQueueAsync(db, invoice, cancellationToken);

                var installment = new Installment
                {
                    HouseholdId = session.HouseholdId,
                    CardPurchaseId = purchase.Id,
                    CreditCardId = card.Id,
                    InvoiceId = invoice.Id,
                    Number = installmentNumber,
                    Amount = amount,
                    DueDate = invoice.DueDate
                };

                await UpsertAndQueueAsync(db, installment, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            await SafeSyncAsync(session, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task PayInvoiceAsync(AuthSession session, InvoicePaymentInput input, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var invoice = await db.Invoices.FirstAsync(x => x.Id == input.InvoiceId, cancellationToken);
            var account = await db.BankAccounts.FirstAsync(x => x.Id == input.BankAccountId, cancellationToken);
            var card = await db.CreditCards.FirstAsync(x => x.Id == invoice.CreditCardId, cancellationToken);

            var paymentAmount = input.Amount > 0 ? input.Amount : Math.Max(invoice.TotalAmount - invoice.PaidAmount, 0);
            invoice.RegisterPayment(paymentAmount, input.PaidOn.ToUniversalTime());
            account.CurrentBalance -= paymentAmount;

            if (invoice.Status == InvoiceStatus.Paid)
            {
                var installments = await db.Installments.Where(x => x.InvoiceId == invoice.Id).ToListAsync(cancellationToken);
                foreach (var installment in installments)
                {
                    installment.IsPaid = true;
                    await UpsertAndQueueAsync(db, installment, cancellationToken);
                }
            }

            var transaction = new Transaction
            {
                HouseholdId = session.HouseholdId,
                OwnerUserId = session.UserId,
                Description = $"Pagamento fatura {card.Name} {invoice.ReferenceLabel}",
                Amount = paymentAmount,
                OccurredOn = input.PaidOn,
                BankAccountId = account.Id,
                CreditCardId = card.Id,
                InvoiceId = invoice.Id,
                Kind = TransactionKind.CardInvoicePayment,
                Scope = EntryScope.Joint
            };

            await QueueEntityAsync(db, account, cancellationToken);
            await UpsertAndQueueAsync(db, invoice, cancellationToken);
            await UpsertAndQueueAsync(db, transaction, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await SafeSyncAsync(session, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveGoalAsync(AuthSession session, GoalInput input, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var entity = input.Id.HasValue
                ? await db.Goals.FirstOrDefaultAsync(x => x.Id == input.Id.Value, cancellationToken) ?? new Goal { Id = input.Id.Value, HouseholdId = session.HouseholdId }
                : new Goal { HouseholdId = session.HouseholdId };

            entity.Name = input.Name.Trim();
            entity.GoalType = input.GoalType;
            entity.TargetAmount = input.TargetAmount;
            entity.CurrentAmount = input.CurrentAmount;
            entity.MonthlyContributionTarget = input.MonthlyContributionTarget;
            entity.TargetDate = input.TargetDate;
            entity.Notes = input.Notes;

            await UpsertAndQueueAsync(db, entity, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await SafeSyncAsync(session, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveInvestmentAssetAsync(AuthSession session, InvestmentAssetInput input, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var entity = input.Id.HasValue
                ? await db.InvestmentAssets.FirstOrDefaultAsync(x => x.Id == input.Id.Value, cancellationToken) ?? new InvestmentAsset { Id = input.Id.Value, HouseholdId = session.HouseholdId, OwnerUserId = session.UserId }
                : new InvestmentAsset { HouseholdId = session.HouseholdId, OwnerUserId = session.UserId };

            entity.Name = input.Name.Trim();
            entity.Ticker = input.Ticker;
            entity.Broker = input.Broker.Trim();
            entity.AssetType = input.AssetType;
            entity.InvestedAmount = input.InvestedAmount;
            entity.CurrentValue = input.CurrentValue;
            entity.CurrentQuantity = input.Quantity;
            entity.Scope = input.Scope;
            entity.UpdatedOn = input.UpdatedOn;

            await UpsertAndQueueAsync(db, entity, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await SafeSyncAsync(session, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveCategoryAsync(AuthSession session, CategoryInput input, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var entity = input.Id.HasValue
                ? await db.Categories.FirstOrDefaultAsync(x => x.Id == input.Id.Value, cancellationToken) ?? new Category { Id = input.Id.Value, HouseholdId = session.HouseholdId }
                : new Category { HouseholdId = session.HouseholdId };

            entity.Name = input.Name.Trim();
            entity.IconKey = input.IconKey.Trim();
            entity.ColorHex = input.ColorHex;
            entity.Type = input.Type;

            await UpsertAndQueueAsync(db, entity, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await SafeSyncAsync(session, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task RefreshInsightsAsync(AuthSession session, DateTime referenceDate, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var categories = await db.Categories.Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).ToListAsync(cancellationToken);
            var transactions = await db.Transactions.Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).ToListAsync(cancellationToken);
            var cards = await db.CreditCards.Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).ToListAsync(cancellationToken);
            var invoices = await db.Invoices.Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).ToListAsync(cancellationToken);
            var goals = await db.Goals.Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).ToListAsync(cancellationToken);
            var investments = await db.InvestmentAssets.Where(x => x.HouseholdId == session.HouseholdId && x.DeletedAtUtc == null).ToListAsync(cancellationToken);
            var generated = insightEngine.GenerateInsights(transactions, categories, cards, invoices, goals, investments, session.HouseholdId, referenceDate);

            var existing = await db.Insights.Where(x => x.HouseholdId == session.HouseholdId).ToListAsync(cancellationToken);
            db.Insights.RemoveRange(existing);
            db.Insights.AddRange(generated);
            foreach (var insight in generated)
            {
                await QueueEntityAsync(db, insight, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            await SafeSyncAsync(session, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task SafeSyncAsync(AuthSession session, CancellationToken cancellationToken)
    {
        try
        {
            await syncCoordinator.SyncAsync(session, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Sync pós-gravação falhou");
        }
    }
}
