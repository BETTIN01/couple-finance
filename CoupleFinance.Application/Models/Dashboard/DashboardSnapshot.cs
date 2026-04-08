namespace CoupleFinance.Application.Models.Dashboard;

public sealed record DashboardSnapshot(
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal CurrentBalance,
    decimal CardUsage,
    IReadOnlyList<TrendPoint> TrendPoints,
    IReadOnlyList<CategoryBreakdown> CategoryBreakdown,
    IReadOnlyList<ComparisonBar> Comparisons);
