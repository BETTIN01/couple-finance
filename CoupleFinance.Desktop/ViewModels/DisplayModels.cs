using MaterialDesignThemes.Wpf;
using System.Windows.Media;

namespace CoupleFinance.Desktop.ViewModels;

public sealed record ContributionCardItem(
    string Title,
    string Subtitle,
    decimal Amount,
    string Caption,
    Brush AccentBrush,
    PackIconKind IconKind);

public sealed record TransactionListItem(
    Guid Id,
    DateTime Date,
    string Description,
    string KindLabel,
    string ScopeLabel,
    string OwnerLabel,
    string AccountLabel,
    string CategoryLabel,
    string SupportingText,
    decimal SignedAmount,
    Brush AccentBrush,
    PackIconKind IconKind);

public sealed record AccountOverviewItem(
    Guid Id,
    string Name,
    string Institution,
    string TypeLabel,
    decimal Balance,
    decimal Inflows,
    decimal Outflows,
    int MovementCount,
    Brush AccentBrush);

public sealed record CardOverviewItem(
    Guid Id,
    string Name,
    string Brand,
    decimal LimitAmount,
    decimal OpenAmount,
    decimal AvailableAmount,
    double UsagePercent,
    string DueText,
    int PurchaseCount,
    Brush AccentBrush);

public sealed record InvoiceOverviewItem(
    Guid Id,
    string CardName,
    string Reference,
    string StatusLabel,
    decimal TotalAmount,
    decimal RemainingAmount,
    DateTime DueDate,
    Brush AccentBrush);

public sealed record GoalOverviewItem(
    Guid GoalId,
    string Title,
    string TypeLabel,
    decimal CurrentAmount,
    decimal TargetAmount,
    decimal RemainingAmount,
    decimal SuggestedMonthlyContribution,
    int MonthsToTarget,
    DateTime? ProjectedCompletionDate,
    double ProgressPercent,
    string Recommendation,
    Brush AccentBrush);

public sealed record InvestmentOverviewItem(
    Guid Id,
    string Name,
    string Ticker,
    string OwnerLabel,
    string ScopeLabel,
    decimal InvestedAmount,
    decimal CurrentValue,
    decimal Profit,
    decimal ProfitPercentage,
    Brush AccentBrush);

public sealed record InvestmentAllocationItem(
    string Title,
    decimal CurrentValue,
    double RatioPercent,
    Brush AccentBrush);

public sealed record InsightOverviewItem(
    Guid Id,
    string Title,
    string Message,
    string SeverityLabel,
    string SupportingText,
    Brush AccentBrush,
    PackIconKind IconKind);

public sealed record CategorySpendItem(
    string Name,
    decimal Amount,
    double RatioPercent,
    Brush AccentBrush);
