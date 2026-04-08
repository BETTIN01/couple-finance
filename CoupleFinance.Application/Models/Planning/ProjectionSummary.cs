namespace CoupleFinance.Application.Models.Planning;

public sealed record ProjectionSummary(
    decimal MonthlyIncome,
    decimal MonthlyExpenses,
    decimal SavingsRate,
    decimal AvailableToSave,
    decimal TotalInvested,
    decimal TotalCurrentInvestments,
    IReadOnlyList<GoalProjection> Goals,
    string Recommendation);
