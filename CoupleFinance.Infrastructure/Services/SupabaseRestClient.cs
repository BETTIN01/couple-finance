using CoupleFinance.Domain.Entities;
using CoupleFinance.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CoupleFinance.Infrastructure.Services;

public sealed class SupabaseRestClient(
    HttpClient httpClient,
    IOptions<SupabaseOptions> options,
    ILogger<SupabaseRestClient> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<RemoteAuthResponse> SignUpAsync(string email, string password, string displayName, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return RemoteAuthResponse.NotConfigured;
        }

        var payload = new
        {
            email,
            password,
            data = new { display_name = displayName }
        };

        return await SendAuthAsync("auth/v1/signup", payload, cancellationToken);
    }

    public async Task<RemoteAuthResponse> SignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return RemoteAuthResponse.NotConfigured;
        }

        var payload = new
        {
            email,
            password
        };

        return await SendAuthAsync("auth/v1/token?grant_type=password", payload, cancellationToken);
    }

    public async Task<RemoteHousehold?> GetHouseholdByInviteCodeAsync(string inviteCode, string? accessToken, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        using var request = CreateRequest(HttpMethod.Get, $"rest/v1/households?invite_code=eq.{Uri.EscapeDataString(inviteCode)}&select=id,name,invite_code,created_by_user_id", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Supabase household lookup failed with {StatusCode}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var households = JsonSerializer.Deserialize<List<RemoteHousehold>>(json, _jsonOptions);
        return households?.FirstOrDefault();
    }

    public async Task UpsertHouseholdAsync(Household household, string? accessToken, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return;
        }

        var payload = new[]
        {
            new
            {
                id = household.Id,
                name = household.Name,
                invite_code = household.InviteCode,
                created_by_user_id = household.CreatedByUserId,
                updated_at_utc = household.UpdatedAtUtc
            }
        };

        await SendRestAsync("rest/v1/households?on_conflict=id", payload, accessToken, cancellationToken);
    }

    public async Task UpsertProfileAsync(UserProfile profile, string? accessToken, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return;
        }

        var payload = new[]
        {
            new
            {
                id = profile.Id,
                household_id = profile.HouseholdId,
                display_name = profile.DisplayName,
                email = profile.Email,
                accent_hex = profile.AccentHex,
                updated_at_utc = profile.UpdatedAtUtc
            }
        };

        await SendRestAsync("rest/v1/profiles?on_conflict=id", payload, accessToken, cancellationToken);
    }

    public async Task UpsertSyncDocumentAsync(RemoteSyncDocument document, string? accessToken, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return;
        }

        var payload = new[]
        {
            new
            {
                household_id = document.HouseholdId,
                entity_name = document.EntityName,
                entity_id = document.EntityId,
                payload = JsonNode.Parse(document.PayloadJson),
                updated_at_utc = document.UpdatedAtUtc,
                deleted_at_utc = document.DeletedAtUtc
            }
        };

        await SendRestAsync("rest/v1/sync_documents?on_conflict=household_id,entity_name,entity_id", payload, accessToken, cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteSyncDocument>> GetChangedDocumentsAsync(Guid householdId, DateTime? sinceUtc, string? accessToken, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return [];
        }

        var query = $"rest/v1/sync_documents?household_id=eq.{householdId}&select=household_id,entity_name,entity_id,payload,updated_at_utc,deleted_at_utc";
        if (sinceUtc.HasValue)
        {
            query += $"&updated_at_utc=gt.{Uri.EscapeDataString(sinceUtc.Value.ToString("O"))}";
        }

        query += "&order=updated_at_utc.asc";
        using var request = CreateRequest(HttpMethod.Get, query, accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Supabase pull failed with {StatusCode}", response.StatusCode);
            return [];
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var documents = JsonSerializer.Deserialize<List<RemoteSyncDocument>>(json, _jsonOptions);
        return documents ?? [];
    }

    private async Task<RemoteAuthResponse> SendAuthAsync(string relativePath, object payload, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, relativePath, null);
            request.Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Supabase auth failed: {Body}", json);
                return new RemoteAuthResponse(false, null, null, null, json);
            }

            var authPayload = JsonNode.Parse(json);
            var accessToken = authPayload?["access_token"]?.GetValue<string>();
            var refreshToken = authPayload?["refresh_token"]?.GetValue<string>();
            var userId = authPayload?["user"]?["id"]?.GetValue<string>();
            return new RemoteAuthResponse(true, accessToken, refreshToken, userId, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Supabase auth request failed");
            return new RemoteAuthResponse(false, null, null, null, ex.Message);
        }
    }

    private async Task SendRestAsync(string relativePath, object payload, string? accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, relativePath, accessToken);
            request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=representation");
            request.Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Supabase REST request failed for {Path}", relativePath);
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, string? accessToken)
    {
        httpClient.BaseAddress = new Uri(options.Value.Url.TrimEnd('/') + "/");
        var request = new HttpRequestMessage(method, relativePath);
        request.Headers.Add("apikey", options.Value.AnonKey);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return request;
    }
}

public sealed record RemoteAuthResponse(bool Succeeded, string? AccessToken, string? RefreshToken, string? UserId, string? ErrorMessage)
{
    public static RemoteAuthResponse NotConfigured => new(true, null, null, null, null);
}

public sealed record RemoteHousehold(
    Guid Id,
    string Name,
    [property: JsonPropertyName("invite_code")] string InviteCode,
    [property: JsonPropertyName("created_by_user_id")] Guid CreatedByUserId);

public sealed record RemoteSyncDocument(
    [property: JsonPropertyName("household_id")] Guid HouseholdId,
    [property: JsonPropertyName("entity_name")] string EntityName,
    [property: JsonPropertyName("entity_id")] Guid EntityId,
    [property: JsonPropertyName("payload")] JsonElement Payload,
    [property: JsonPropertyName("updated_at_utc")] DateTime UpdatedAtUtc,
    [property: JsonPropertyName("deleted_at_utc")] DateTime? DeletedAtUtc)
{
    public string PayloadJson => Payload.GetRawText();
}
