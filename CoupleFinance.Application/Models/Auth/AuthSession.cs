namespace CoupleFinance.Application.Models.Auth;

public sealed record AuthSession(
    Guid UserId,
    Guid HouseholdId,
    string DisplayName,
    string Email,
    string HouseholdName,
    bool IsOfflineMode,
    string? AccessToken,
    string? RefreshToken);
