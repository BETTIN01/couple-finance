namespace CoupleFinance.Application.Models.Sync;

public sealed record SyncResult(
    bool Succeeded,
    int UploadedCount,
    int DownloadedCount,
    DateTime FinishedAtUtc,
    string? ErrorMessage);
