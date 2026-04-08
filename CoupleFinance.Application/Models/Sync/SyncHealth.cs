namespace CoupleFinance.Application.Models.Sync;

public sealed record SyncHealth(
    bool IsConfigured,
    bool IsOnline,
    int PendingItems,
    DateTime? LastSyncAtUtc,
    string StatusText,
    string? LastError);
