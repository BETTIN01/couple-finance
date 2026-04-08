using CoupleFinance.Application.Models.Auth;

namespace CoupleFinance.Application.Contracts;

public interface IAuthService
{
    Task<AuthResult> SignInAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthSession?> RestoreSessionAsync(CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
    Task<string> GetInviteCodeAsync(Guid householdId, CancellationToken cancellationToken = default);
}
