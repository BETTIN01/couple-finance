namespace CoupleFinance.Desktop.Configuration;

public sealed class SyncAutomationOptions
{
    public bool Enabled { get; set; } = true;
    public bool SyncOnStartup { get; set; } = true;
    public bool SyncAfterLocalChanges { get; set; } = true;
    public bool RefreshAfterAutomaticSync { get; set; } = true;
    public int IntervalSeconds { get; set; } = 15;
}
