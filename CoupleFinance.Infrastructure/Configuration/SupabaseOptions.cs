namespace CoupleFinance.Infrastructure.Configuration;

public sealed class SupabaseOptions
{
    public string Url { get; set; } = string.Empty;
    public string AnonKey { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Url) &&
        !string.IsNullOrWhiteSpace(AnonKey);
}
