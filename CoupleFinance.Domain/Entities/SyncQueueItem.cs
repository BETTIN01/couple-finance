using CoupleFinance.Domain.Common;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Domain.Entities;

public sealed class SyncQueueItem : EntityBase
{
    public Guid HouseholdId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public SyncOperation Operation { get; set; } = SyncOperation.Upsert;
    public string PayloadJson { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
}
