using CoupleFinance.Domain.Common;

namespace CoupleFinance.Domain.Entities;

public sealed class ProjectionSnapshot : SyncEntity
{
    public int ReferenceMonth { get; set; }
    public int ReferenceYear { get; set; }
    public decimal ProjectedBalance { get; set; }
    public decimal ProjectedSavingsRate { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string SummaryJson { get; set; } = string.Empty;
}
