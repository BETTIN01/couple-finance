namespace CoupleFinance.Desktop.Models;

public sealed class AppUpdateManifest
{
    public string Version { get; set; } = string.Empty;
    public string InstallerUrl { get; set; } = string.Empty;
    public string? PortableUrl { get; set; }
    public string? PortableSha256 { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? Sha256 { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public bool Mandatory { get; set; }
}
