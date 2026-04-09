namespace CoupleFinance.Desktop.Configuration;

public sealed class UpdateOptions
{
    public bool Enabled { get; set; }
    public bool CheckOnStartup { get; set; } = true;
    public bool AutoInstallOnStartup { get; set; } = true;
    public int PeriodicCheckIntervalMinutes { get; set; } = 15;
    public bool PreferBackgroundPackage { get; set; } = false;
    public bool RelaunchAfterInstall { get; set; } = true;
    public string ManifestUrl { get; set; } = string.Empty;
    public int StartupDelaySeconds { get; set; } = 3;
    public string DownloadFolderName { get; set; } = "Updates";
}
