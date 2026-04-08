namespace CoupleFinance.Application.Models.Auth;

public sealed record AuthResult(
    bool Succeeded,
    string? ErrorMessage,
    AuthSession? Session,
    string? InviteCode = null);
