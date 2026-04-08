using CoupleFinance.Domain.Common;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Domain.Entities;

public sealed class Insight : SyncEntity
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public InsightSeverity Severity { get; set; } = InsightSeverity.Neutral;
    public decimal MetricDeltaPercentage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
