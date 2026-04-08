namespace CoupleFinance.Domain.Enums;

public enum SyncStatus
{
    LocalOnly = 0,
    PendingSync = 1,
    Synced = 2,
    Conflict = 3
}
