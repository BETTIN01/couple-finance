using CoupleFinance.Domain.Common;

namespace CoupleFinance.Domain.Entities;

public sealed class UserProfile : SyncEntity
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string PreferredCurrency { get; set; } = "BRL";
    public string AccentHex { get; set; } = "#FFB45E";
    public bool IsDemoUser { get; set; }
}
