using CoupleFinance.Application.Models.Auth;
using CoupleFinance.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CoupleFinance.Infrastructure.Services;

public sealed class AppSessionStore(IOptions<LocalStorageOptions> options)
{
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string SessionFilePath => Path.Combine(GetAppFolder(), options.Value.SessionFileName);

    public async Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(GetAppFolder());
        await File.WriteAllTextAsync(SessionFilePath, JsonSerializer.Serialize(session, _serializerOptions), cancellationToken);
    }

    public async Task<AuthSession?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SessionFilePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(SessionFilePath, cancellationToken);
        return JsonSerializer.Deserialize<AuthSession>(json, _serializerOptions);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(SessionFilePath))
        {
            File.Delete(SessionFilePath);
        }

        return Task.CompletedTask;
    }

    public string GetAppFolder()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), options.Value.AppFolderName);
        Directory.CreateDirectory(root);
        return root;
    }
}
