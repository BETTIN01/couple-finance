using CoupleFinance.Application.Models;
using CoupleFinance.Application.Models.Auth;
using CoupleFinance.Application.Models.Dashboard;

namespace CoupleFinance.Application.Contracts;

public interface IFinanceWorkspaceService
{
    Task InitializeAsync(AuthSession session, CancellationToken cancellationToken = default);
    Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync(AuthSession session, PeriodFilter filter, CancellationToken cancellationToken = default);
    Task SaveBankAccountAsync(AuthSession session, BankAccountInput input, CancellationToken cancellationToken = default);
    Task SaveTransactionAsync(AuthSession session, TransactionInput input, CancellationToken cancellationToken = default);
    Task SaveTransferAsync(AuthSession session, TransferInput input, CancellationToken cancellationToken = default);
    Task SaveCreditCardAsync(AuthSession session, CreditCardInput input, CancellationToken cancellationToken = default);
    Task SaveCardPurchaseAsync(AuthSession session, CardPurchaseInput input, CancellationToken cancellationToken = default);
    Task PayInvoiceAsync(AuthSession session, InvoicePaymentInput input, CancellationToken cancellationToken = default);
    Task SaveGoalAsync(AuthSession session, GoalInput input, CancellationToken cancellationToken = default);
    Task SaveInvestmentAssetAsync(AuthSession session, InvestmentAssetInput input, CancellationToken cancellationToken = default);
    Task SaveCategoryAsync(AuthSession session, CategoryInput input, CancellationToken cancellationToken = default);
    Task RefreshInsightsAsync(AuthSession session, DateTime referenceDate, CancellationToken cancellationToken = default);
}
