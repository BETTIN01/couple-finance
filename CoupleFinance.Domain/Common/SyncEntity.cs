using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Domain.Common;

public abstract class SyncEntity : EntityBase
{
    public Guid HouseholdId { get; set; }
    public string? RemoteId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
    public SyncStatus SyncStatus { get; set; } = SyncStatus.PendingSync;
    public DateTime? LastSyncedAtUtc { get; set; }

    public virtual void Touch()
    {
        UpdatedAtUtc = DateTime.UtcNow;
        if (DeletedAtUtc is null)
        {
            SyncStatus = SyncStatus.PendingSync;
        }
    }

    public virtual void MarkSynced(DateTime syncedAtUtc)
    {
        LastSyncedAtUtc = syncedAtUtc;
        SyncStatus = SyncStatus.Synced;
    }

    public virtual void SoftDelete()
    {
        DeletedAtUtc = DateTime.UtcNow;
        Touch();
    }
}
