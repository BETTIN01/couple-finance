using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Application.Models;

public sealed record BankAccountInput(
    Guid? Id,
    string Name,
    string Institution,
    AccountType Type,
    decimal CurrentBalance,
    string ColorHex);

public sealed record TransactionInput(
    Guid? Id,
    string Description,
    decimal Amount,
    DateTime OccurredOn,
    Guid? CategoryId,
    Guid? BankAccountId,
    TransactionKind Kind,
    EntryScope Scope,
    string? Notes);

public sealed record TransferInput(
    Guid? Id,
    Guid FromBankAccountId,
    Guid ToBankAccountId,
    decimal Amount,
    DateTime OccurredOn,
    string Description);

public sealed record CreditCardInput(
    Guid? Id,
    string Name,
    string Brand,
    decimal LimitAmount,
    int ClosingDay,
    int DueDay,
    string ColorHex);

public sealed record CardPurchaseInput(
    Guid? Id,
    Guid CreditCardId,
    string Description,
    decimal Amount,
    DateTime PurchaseDate,
    int InstallmentCount,
    Guid? CategoryId,
    EntryScope Scope,
    string? Notes);

public sealed record InvoicePaymentInput(
    Guid InvoiceId,
    Guid BankAccountId,
    decimal Amount,
    DateTime PaidOn);

public sealed record GoalInput(
    Guid? Id,
    string Name,
    GoalType GoalType,
    decimal TargetAmount,
    decimal CurrentAmount,
    decimal MonthlyContributionTarget,
    DateTime? TargetDate,
    string? Notes);

public sealed record InvestmentAssetInput(
    Guid? Id,
    string Name,
    string? Ticker,
    string Broker,
    InvestmentAssetType AssetType,
    decimal InvestedAmount,
    decimal CurrentValue,
    decimal Quantity,
    EntryScope Scope,
    DateTime UpdatedOn);

public sealed record CategoryInput(
    Guid? Id,
    string Name,
    string IconKey,
    string ColorHex,
    CategoryType Type);
