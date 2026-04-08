namespace CoupleFinance.Application.Models.Auth;

public sealed record RegisterRequest(
    string DisplayName,
    string Email,
    string Password,
    string? HouseholdName,
    string? InviteCode);
