namespace CoupleFinance.Infrastructure.Configuration;

public sealed class LocalStorageOptions
{
    public string AppFolderName { get; set; } = "CoupleFinance";
    public string DatabaseFileName { get; set; } = "couplefinance.db";
    public string SessionFileName { get; set; } = "session.json";
}
