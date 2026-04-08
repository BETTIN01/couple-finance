using CoupleFinance.Application.Contracts;
using CoupleFinance.Application.Models.Auth;
using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;
using CoupleFinance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoupleFinance.Infrastructure.Services;

public sealed class AuthService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    AppSessionStore sessionStore,
    PasswordHasher passwordHasher,
    SupabaseRestClient supabaseClient,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResult> SignInAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.UserProfiles.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (user is null)
        {
            return new AuthResult(false, "Usuário não encontrado.", null);
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return new AuthResult(false, "Senha inválida.", null);
        }

        var household = await db.Households.FirstAsync(x => x.Id == user.HouseholdId, cancellationToken);
        var remoteAuth = await supabaseClient.SignInAsync(normalizedEmail, request.Password, cancellationToken);
        var session = new AuthSession(
            user.Id,
            household.Id,
            user.DisplayName,
            user.Email,
            household.Name,
            !remoteAuth.Succeeded && supabaseClient.IsConfigured,
            remoteAuth.AccessToken,
            remoteAuth.RefreshToken);

        await sessionStore.SaveAsync(session, cancellationToken);
        return new AuthResult(true, null, session, household.InviteCode);
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await db.UserProfiles.AnyAsync(x => x.Email == normalizedEmail, cancellationToken))
        {
            return new AuthResult(false, "Já existe uma conta com este e-mail.", null);
        }

        Household household;
        HouseholdMemberRole role;

        if (!string.IsNullOrWhiteSpace(request.InviteCode))
        {
            Household? foundHousehold = await db.Households.FirstOrDefaultAsync(x => x.InviteCode == request.InviteCode.Trim(), cancellationToken)
                ?? await CreateHouseholdFromRemoteAsync(request.InviteCode.Trim(), cancellationToken);

            if (foundHousehold is null)
            {
                return new AuthResult(false, "Código de convite inválido.", null);
            }

            household = foundHousehold;
            role = HouseholdMemberRole.Partner;
            if (!await db.Households.AnyAsync(x => x.Id == household.Id, cancellationToken))
            {
                db.Households.Add(household);
            }
        }
        else
        {
            household = new Household
            {
                Name = string.IsNullOrWhiteSpace(request.HouseholdName) ? $"Casa de {request.DisplayName}" : request.HouseholdName.Trim(),
                InviteCode = GenerateInviteCode(),
                CreatedByUserId = Guid.Empty
            };

            role = HouseholdMemberRole.Primary;
            db.Households.Add(household);
        }

        var (hash, salt) = passwordHasher.HashPassword(request.Password);
        var user = new UserProfile
        {
            HouseholdId = household.Id,
            DisplayName = request.DisplayName.Trim(),
            Email = normalizedEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            AccentHex = role == HouseholdMemberRole.Primary ? "#FFF5A623" : "#FF7C5DFA"
        };

        if (role == HouseholdMemberRole.Primary)
        {
            household.CreatedByUserId = user.Id;
        }

        var member = new HouseholdMember
        {
            HouseholdId = household.Id,
            UserProfileId = user.Id,
            Role = role
        };

        db.UserProfiles.Add(user);
        db.HouseholdMembers.Add(member);
        await db.SaveChangesAsync(cancellationToken);

        var remoteAuth = await supabaseClient.SignUpAsync(user.Email, request.Password, user.DisplayName, cancellationToken);
        await supabaseClient.UpsertHouseholdAsync(household, remoteAuth.AccessToken, cancellationToken);
        await supabaseClient.UpsertProfileAsync(user, remoteAuth.AccessToken, cancellationToken);

        var session = new AuthSession(
            user.Id,
            household.Id,
            user.DisplayName,
            user.Email,
            household.Name,
            !remoteAuth.Succeeded && supabaseClient.IsConfigured,
            remoteAuth.AccessToken,
            remoteAuth.RefreshToken);

        await sessionStore.SaveAsync(session, cancellationToken);
        return new AuthResult(true, null, session, household.InviteCode);
    }

    public async Task<AuthSession?> RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        var session = await sessionStore.LoadAsync(cancellationToken);
        if (session is null)
        {
            return null;
        }

        await EnsureDatabaseAsync(cancellationToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var isValid = await db.UserProfiles.AnyAsync(x => x.Id == session.UserId, cancellationToken)
            && await db.Households.AnyAsync(x => x.Id == session.HouseholdId, cancellationToken);

        return isValid ? session : null;
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default) => sessionStore.ClearAsync(cancellationToken);

    public async Task<string> GetInviteCodeAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        await EnsureDatabaseAsync(cancellationToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Households.Where(x => x.Id == householdId).Select(x => x.InviteCode).FirstAsync(cancellationToken);
    }

    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    private async Task<Household?> CreateHouseholdFromRemoteAsync(string inviteCode, CancellationToken cancellationToken)
    {
        try
        {
            var remote = await supabaseClient.GetHouseholdByInviteCodeAsync(inviteCode, null, cancellationToken);
            if (remote is null)
            {
                return null;
            }

            return new Household
            {
                Id = remote.Id,
                HouseholdId = remote.Id,
                Name = remote.Name,
                InviteCode = remote.InviteCode,
                CreatedByUserId = remote.CreatedByUserId,
                SyncStatus = SyncStatus.Synced,
                LastSyncedAtUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao buscar household remoto por convite");
            return null;
        }
    }

    private static string GenerateInviteCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> buffer = stackalloc char[6];
        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] = alphabet[Random.Shared.Next(alphabet.Length)];
        }

        return new string(buffer);
    }
}
