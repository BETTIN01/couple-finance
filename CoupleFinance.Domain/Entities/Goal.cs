using CoupleFinance.Domain.Common;
using CoupleFinance.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoupleFinance.Domain.Entities;

public sealed class Goal : SyncEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public GoalType GoalType { get; set; } = GoalType.Other;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public decimal MonthlyContributionTarget { get; set; }
    public DateTime? TargetDate { get; set; }

    [NotMapped]
    public decimal ProgressPercentage =>
        TargetAmount <= 0 ? 0 : Math.Round(CurrentAmount / TargetAmount * 100m, 2, MidpointRounding.AwayFromZero);
}
