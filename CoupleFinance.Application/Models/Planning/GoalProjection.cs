namespace CoupleFinance.Application.Models.Planning;

public sealed record GoalProjection(
    Guid GoalId,
    string GoalName,
    decimal CurrentAmount,
    decimal TargetAmount,
    decimal ProgressPercentage,
    decimal SuggestedMonthlyContribution,
    int MonthsToTarget,
    DateTime? ProjectedCompletionDate,
    string Message);
