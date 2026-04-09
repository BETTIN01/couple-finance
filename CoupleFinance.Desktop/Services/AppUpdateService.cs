using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CoupleFinance.Desktop.Configuration;
using CoupleFinance.Desktop.Models;
using CoupleFinance.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoupleFinance.Desktop.Services;

public sealed partial class AppUpdateService : ObservableObject, IDisposable
{
    private readonly AppSessionStore _sessionStore;
    private readonly IOptionsMonitor<UpdateOptions> _optionsMonitor;
    private readonly ILogger<AppUpdateService> _logger;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IDisposable? _optionsSubscription;

    private string? _installerSource;
    private string? _expectedSha256;
    private string? _portableSource;
    private string? _expectedPortableSha256;

    [ObservableProperty] private bool isEnabled;
    [ObservableProperty] private bool isConfigured;
    [ObservableProperty] private bool isChecking;
    [ObservableProperty] private bool isDownloading;
    [ObservableProperty] private bool isInstalling;
    [ObservableProperty] private bool isUpdateAvailable;
    [ObservableProperty] private string currentVersion = GetCurrentVersionText();
    [ObservableProperty] private string latestVersion = "Nao verificado";
    [ObservableProperty] private string statusText = "Atualizacoes automaticas desativadas.";
    [ObservableProperty] private string releaseNotes = string.Empty;
    [ObservableProperty] private string manifestUrl = string.Empty;
    [ObservableProperty] private DateTimeOffset? publishedAtUtc;

    public AppUpdateService(
        AppSessionStore sessionStore,
        IOptionsMonitor<UpdateOptions> optionsMonitor,
        ILogger<AppUpdateService> logger)
    {
        _sessionStore = sessionStore;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        ApplyOptions(optionsMonitor.CurrentValue);
        _optionsSubscription = optionsMonitor.OnChange(ApplyOptions);
    }

    public bool CheckOnStartup => IsEnabled && _optionsMonitor.CurrentValue.CheckOnStartup && IsConfigured;
    public bool AutoInstallOnStartup => IsEnabled && _optionsMonitor.CurrentValue.AutoInstallOnStartup && IsConfigured;
    public bool AutoInstallEnabled => IsEnabled && _optionsMonitor.CurrentValue.AutoInstallOnStartup && IsConfigured;
    public bool PeriodicChecksEnabled => IsEnabled && IsConfigured && _optionsMonitor.CurrentValue.PeriodicCheckIntervalMinutes > 0;
    public int StartupDelaySeconds => Math.Max(0, _optionsMonitor.CurrentValue.StartupDelaySeconds);
    public TimeSpan PeriodicCheckInterval => TimeSpan.FromMinutes(Math.Max(5, _optionsMonitor.CurrentValue.PeriodicCheckIntervalMinutes));

    public bool CanInstallUpdate =>
        IsUpdateAvailable &&
        (!string.IsNullOrWhiteSpace(_installerSource) || !string.IsNullOrWhiteSpace(_portableSource)) &&
        !IsChecking &&
        !IsDownloading &&
        !IsInstalling;

    partial void OnIsCheckingChanged(bool value) => OnPropertyChanged(nameof(CanInstallUpdate));
    partial void OnIsDownloadingChanged(bool value) => OnPropertyChanged(nameof(CanInstallUpdate));
    partial void OnIsInstallingChanged(bool value) => OnPropertyChanged(nameof(CanInstallUpdate));
    partial void OnIsUpdateAvailableChanged(bool value) => OnPropertyChanged(nameof(CanInstallUpdate));
    partial void OnIsEnabledChanged(bool value) => NotifySummaryCardChanged();
    partial void OnIsConfiguredChanged(bool value) => NotifySummaryCardChanged();
    partial void OnLatestVersionChanged(string value) => NotifySummaryCardChanged();
    partial void OnPublishedAtUtcChanged(DateTimeOffset? value) => NotifySummaryCardChanged();

    public string LatestVersionCardTitle => IsEnabled && IsConfigured
        ? "Versao mais recente"
        : "Canal de atualizacao";

    public string LatestVersionCardValue => !IsEnabled
        ? "Desativado"
        : !IsConfigured
            ? "Nao configurado"
            : LatestVersion;

    public string LatestVersionCardHint => !IsEnabled
        ? "Esta distribuicao foi gerada com as atualizacoes automaticas desligadas."
        : !IsConfigured
            ? "Esta instalacao nao recebeu uma URL publica de atualizacao. Sem isso, outro computador nao descobre novas versoes sozinho."
            : PublishedAtUtc.HasValue
                ? $"Publicado em {PublishedAtUtc.Value.LocalDateTime:dd/MM/yyyy HH:mm}"
                : "Ultima versao publicada no canal remoto configurado.";

    public async Task<bool> CheckForUpdatesAsync(bool background = false, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            CurrentVersion = GetCurrentVersionText();
            ApplyOptions(_optionsMonitor.CurrentValue);

            if (!IsEnabled)
            {
                ResetAvailableUpdate();
                LatestVersion = "Desativado";
                PublishedAtUtc = null;
                StatusText = "Atualizacoes automaticas desativadas nesta distribuicao.";
                return false;
            }

            if (!IsConfigured)
            {
                ResetAvailableUpdate();
                LatestVersion = "Nao configurado";
                PublishedAtUtc = null;
                StatusText = "Esta instalacao nao possui um canal remoto de atualizacao. Publique o update-manifest.json em uma URL HTTPS para atualizar outras maquinas automaticamente.";
                return false;
            }

            IsChecking = true;
            StatusText = "Verificando novas versoes...";

            await using var stream = await OpenSourceReadAsync(ManifestUrl, cancellationToken);
            var manifest = await JsonSerializer.DeserializeAsync<AppUpdateManifest>(stream, _jsonOptions, cancellationToken);

            if (manifest is null || !TryParseVersion(manifest.Version, out var newestVersion))
            {
                ResetAvailableUpdate();
                LatestVersion = "Indisponivel";
                PublishedAtUtc = null;
                StatusText = "Manifesto de atualizacao invalido.";
                return false;
            }

            if (!TryParseVersion(CurrentVersion, out var currentVersion))
            {
                currentVersion = new Version(0, 0, 0);
            }

            LatestVersion = NormalizeVersionText(newestVersion);
            ReleaseNotes = manifest.ReleaseNotes?.Trim() ?? string.Empty;
            PublishedAtUtc = manifest.PublishedAtUtc;
            _portableSource = NormalizeSource(manifest.PortableUrl);
            _expectedPortableSha256 = NormalizeHash(manifest.PortableSha256);

            if (newestVersion <= currentVersion)
            {
                ResetAvailableUpdate();
                StatusText = $"Voce ja esta na versao mais recente ({CurrentVersion}).";
                return false;
            }

            _installerSource = NormalizeSource(manifest.InstallerUrl);
            _expectedSha256 = NormalizeHash(manifest.Sha256);

            if (string.IsNullOrWhiteSpace(_installerSource) && string.IsNullOrWhiteSpace(_portableSource))
            {
                ResetAvailableUpdate();
                StatusText = "O manifesto nao informa nenhum pacote valido de atualizacao.";
                return false;
            }

            IsUpdateAvailable = true;
            StatusText = CanApplyBackgroundPackage()
                ? $"Nova versao {LatestVersion} pronta para atualizar em segundo plano."
                : $"Nova versao {LatestVersion} pronta para instalar automaticamente.";
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao verificar atualizacoes.");
            StatusText = background
                ? "Nao foi possivel verificar atualizacoes agora."
                : $"Falha ao verificar atualizacoes: {ex.Message}";
            return false;
        }
        finally
        {
            IsChecking = false;
            _gate.Release();
        }
    }

    public async Task<bool> DownloadAndInstallAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!CanInstallUpdate)
            {
                StatusText = "Nenhuma atualizacao disponivel para instalar.";
                return false;
            }

            IsDownloading = true;
            StatusText = $"Baixando versao {LatestVersion}...";

            var downloadFolder = GetDownloadFolder();
            Directory.CreateDirectory(downloadFolder);
            var currentExecutablePath = GetCurrentExecutablePath();

            if (CanApplyBackgroundPackage() && !string.IsNullOrWhiteSpace(_portableSource))
            {
                StatusText = $"Baixando pacote da versao {LatestVersion}...";
                CleanupOldDownloads(downloadFolder, "CoupleFinance-portable-*.zip");
                var packagePath = CreateDownloadTargetPath(downloadFolder, "CoupleFinance-portable", ".zip", LatestVersion);
                await DownloadSourceAsync(_portableSource, packagePath, cancellationToken);

                if (!await ValidateExpectedHashAsync(packagePath, _expectedPortableSha256, cancellationToken))
                {
                    File.Delete(packagePath);
                    StatusText = "A validacao do pacote de atualizacao falhou. O download foi descartado.";
                    return false;
                }

                IsDownloading = false;
                IsInstalling = true;
                StatusText = "Aplicando atualizacao em segundo plano...";
                LaunchPortableUpdateAfterExit(packagePath, currentExecutablePath, AppContext.BaseDirectory);
                return true;
            }

            if (string.IsNullOrWhiteSpace(_installerSource))
            {
                StatusText = "Nenhum instalador de contingencia foi informado para esta atualizacao.";
                return false;
            }

            StatusText = $"Baixando instalador silencioso da versao {LatestVersion}...";
            CleanupOldDownloads(downloadFolder, "CoupleFinance-Setup-*.exe");
            var installerPath = CreateDownloadTargetPath(downloadFolder, "CoupleFinance-Setup", ".exe", LatestVersion);
            await DownloadSourceAsync(_installerSource, installerPath, cancellationToken);

            if (!await ValidateExpectedHashAsync(installerPath, _expectedSha256, cancellationToken))
            {
                File.Delete(installerPath);
                StatusText = "A validacao do instalador falhou. O download foi descartado.";
                return false;
            }

            IsDownloading = false;
            IsInstalling = true;
            StatusText = "Instalando atualizacao em segundo plano...";
            var installDirectory = GetPreferredInstallDirectory();
            var installedExecutablePath = Path.Combine(installDirectory, Path.GetFileName(currentExecutablePath));
            LaunchInstallerAfterExit(installerPath, installedExecutablePath, installDirectory);
            return true;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Falha ao preparar a atualizacao por arquivo bloqueado.");
            StatusText = "Nao foi possivel preparar a atualizacao porque um arquivo anterior ainda esta em uso. Feche o app, aguarde alguns segundos e tente novamente.";
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao preparar a atualizacao.");
            StatusText = $"Falha ao preparar a atualizacao: {ex.Message}";
            return false;
        }
        finally
        {
            if (!IsInstalling)
            {
                IsDownloading = false;
            }

            _gate.Release();
        }
    }

    public void Dispose()
    {
        _optionsSubscription?.Dispose();
        _httpClient.Dispose();
        _gate.Dispose();
    }

    private void ApplyOptions(UpdateOptions options)
    {
        IsEnabled = options.Enabled;
        ManifestUrl = options.ManifestUrl?.Trim() ?? string.Empty;
        IsConfigured = IsEnabled && !string.IsNullOrWhiteSpace(ManifestUrl);

        if (!IsEnabled && !IsChecking && !IsDownloading && !IsInstalling)
        {
            LatestVersion = "Desativado";
            PublishedAtUtc = null;
            StatusText = "Atualizacoes automaticas desativadas nesta distribuicao.";
        }
        else if (IsEnabled && !IsConfigured && !IsChecking && !IsDownloading && !IsInstalling)
        {
            LatestVersion = "Nao configurado";
            PublishedAtUtc = null;
            StatusText = "Esta instalacao nao possui um canal remoto de atualizacao. Publique o update-manifest.json em uma URL HTTPS para atualizar outras maquinas automaticamente.";
        }

        OnPropertyChanged(nameof(CheckOnStartup));
        OnPropertyChanged(nameof(AutoInstallOnStartup));
        OnPropertyChanged(nameof(AutoInstallEnabled));
        OnPropertyChanged(nameof(PeriodicChecksEnabled));
        OnPropertyChanged(nameof(PeriodicCheckInterval));
    }

    private void ResetAvailableUpdate()
    {
        _installerSource = null;
        _expectedSha256 = null;
        _portableSource = null;
        _expectedPortableSha256 = null;
        IsUpdateAvailable = false;
    }

    private string GetDownloadFolder() => Path.Combine(
        _sessionStore.GetAppFolder(),
        string.IsNullOrWhiteSpace(_optionsMonitor.CurrentValue.DownloadFolderName)
            ? "Updates"
            : _optionsMonitor.CurrentValue.DownloadFolderName);

    private async Task<Stream> OpenSourceReadAsync(string source, CancellationToken cancellationToken)
    {
        if (TryGetWebUri(source, out var webUri))
        {
            var response = await _httpClient.GetAsync(webUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }

        var filePath = ResolveLocalPath(source);
        return File.OpenRead(filePath);
    }

    private async Task DownloadSourceAsync(string source, string destinationPath, CancellationToken cancellationToken)
    {
        if (TryGetWebUri(source, out var webUri))
        {
            using var response = await _httpClient.GetAsync(webUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destinationStream = File.Create(destinationPath);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            await destinationStream.FlushAsync(cancellationToken);
            return;
        }

        var filePath = ResolveLocalPath(source);
        File.Copy(filePath, destinationPath, overwrite: true);
    }

    private void LaunchInstallerAfterExit(string installerPath, string executablePath, string targetDirectory)
    {
        var scriptPath = EnsureBackgroundUpdateScript();
        var process = StartBackgroundUpdater(
            scriptPath,
            $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -ParentProcessId {Environment.ProcessId} -Mode installer -SourcePath \"{installerPath}\" -ExecutablePath \"{executablePath}\" -TargetDirectory \"{targetDirectory}\" -InstallerDirectory \"{targetDirectory}\" -RelaunchAfterInstall:${_optionsMonitor.CurrentValue.RelaunchAfterInstall.ToString().ToLowerInvariant()}");

        if (process is null)
        {
            throw new InvalidOperationException("Não foi possível iniciar o assistente de atualização.");
        }

        System.Windows.Application.Current?.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
    }

    private void LaunchPortableUpdateAfterExit(string packagePath, string executablePath, string targetDirectory)
    {
        var scriptPath = EnsureBackgroundUpdateScript();
        var process = StartBackgroundUpdater(
            scriptPath,
            $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -ParentProcessId {Environment.ProcessId} -Mode package -SourcePath \"{packagePath}\" -ExecutablePath \"{executablePath}\" -TargetDirectory \"{targetDirectory}\" -RelaunchAfterInstall:${_optionsMonitor.CurrentValue.RelaunchAfterInstall.ToString().ToLowerInvariant()}");

        if (process is null)
        {
            throw new InvalidOperationException("Não foi possível iniciar o atualizador em segundo plano.");
        }

        System.Windows.Application.Current?.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
    }

    private string EnsureBackgroundUpdateScript()
    {
        var scriptPath = Path.Combine(GetDownloadFolder(), "apply-background-update.ps1");
        File.WriteAllText(scriptPath, BuildBackgroundUpdateScript(), Encoding.UTF8);
        return scriptPath;
    }

    private static string BuildBackgroundUpdateScript() => """
param(
    [int]$ParentProcessId,
    [ValidateSet('installer', 'package')]
    [string]$Mode,
    [string]$SourcePath,
    [string]$ExecutablePath,
    [string]$TargetDirectory,
    [string]$InstallerDirectory = "",
    [bool]$RelaunchAfterInstall = $true
)

$logPath = Join-Path (Split-Path -Parent $SourcePath) "update.log"

function Write-Log {
    param([string]$Message)
    Add-Content -Path $logPath -Value ("[{0}] {1}" -f (Get-Date -Format s), $Message) -Encoding UTF8
}

function Wait-ForPath {
    param(
        [string]$Path,
        [int]$TimeoutSeconds = 90
    )

    for ($second = 0; $second -lt $TimeoutSeconds; $second++) {
        if (Test-Path -LiteralPath $Path) {
            return $true
        }

        Start-Sleep -Seconds 1
    }

    return $false
}

try {
    Wait-Process -Id $ParentProcessId -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    if ($Mode -eq "package") {
        New-Item -ItemType Directory -Path $TargetDirectory -Force | Out-Null
        $extractDirectory = Join-Path (Split-Path -Parent $SourcePath) ("package-" + [Guid]::NewGuid().ToString("N"))
        Expand-Archive -LiteralPath $SourcePath -DestinationPath $extractDirectory -Force

        $sourceRoot = $extractDirectory
        $entries = @(Get-ChildItem -LiteralPath $extractDirectory)
        if ($entries.Count -eq 1 -and $entries[0].PSIsContainer) {
            $sourceRoot = $entries[0].FullName
        }

        Copy-Item -Path (Join-Path $sourceRoot "*") -Destination $TargetDirectory -Recurse -Force
        Remove-Item -LiteralPath $extractDirectory -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $SourcePath -Force -ErrorAction SilentlyContinue
        Write-Log "Atualizacao por pacote concluida."
    }
    else {
        $installerArguments = @('/SP-', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
        if ($InstallerDirectory) {
            $installerArguments += "/DIR=$InstallerDirectory"
        }

        $installerProcess = Start-Process -FilePath $SourcePath -ArgumentList $installerArguments -PassThru -Wait -WindowStyle Hidden
        Write-Log ("Instalador finalizado com codigo " + $installerProcess.ExitCode)
        Remove-Item -LiteralPath $SourcePath -Force -ErrorAction SilentlyContinue
        [void](Wait-ForPath -Path $ExecutablePath)
    }

    if ($RelaunchAfterInstall -and (Test-Path $ExecutablePath)) {
        Start-Process -FilePath $ExecutablePath | Out-Null
        Write-Log "Aplicacao reaberta apos atualizar."
    }
}
catch {
    Write-Log $_.Exception.ToString()
}
""";

    private static Process? StartBackgroundUpdater(string scriptPath, string arguments) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? AppContext.BaseDirectory
        });

    private static bool TryGetWebUri(string source, out Uri uri)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var parsedUri) || parsedUri is null)
        {
            uri = null!;
            return false;
        }

        uri = parsedUri;
        return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
               uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveLocalPath(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return uri.LocalPath;
        }

        if (Path.IsPathRooted(source))
        {
            return source;
        }

        return Path.GetFullPath(source, AppContext.BaseDirectory);
    }

    private static string? NormalizeSource(string? source) =>
        string.IsNullOrWhiteSpace(source)
            ? null
            : source.Trim();

    private static string BuildInstallerFileName(string source, string versionText)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            var uriFileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(uriFileName))
            {
                return uriFileName;
            }
        }

        var localFileName = Path.GetFileName(source);
        if (!string.IsNullOrWhiteSpace(localFileName))
        {
            return localFileName;
        }

        return $"CoupleFinance-Setup-{versionText}.exe";
    }

    private static string BuildPackageFileName(string source, string versionText)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            var uriFileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(uriFileName))
            {
                return uriFileName;
            }
        }

        var localFileName = Path.GetFileName(source);
        if (!string.IsNullOrWhiteSpace(localFileName))
        {
            return localFileName;
        }

        return $"CoupleFinance-portable-{versionText}.zip";
    }

    private static string CreateDownloadTargetPath(string folderPath, string baseName, string extension, string versionText)
    {
        var normalizedVersion = string.IsNullOrWhiteSpace(versionText)
            ? DateTime.UtcNow.ToString("yyyyMMddHHmmss")
            : versionText.Trim().Replace(' ', '-');

        var primaryCandidate = Path.Combine(folderPath, $"{baseName}-{normalizedVersion}{extension}");
        if (TryResetDownloadTarget(primaryCandidate))
        {
            return primaryCandidate;
        }

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var candidate = Path.Combine(folderPath, $"{baseName}-{normalizedVersion}-{attempt}{extension}");
            if (TryResetDownloadTarget(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(folderPath, $"{baseName}-{normalizedVersion}-{Guid.NewGuid():N}{extension}");
    }

    private static bool TryResetDownloadTarget(string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void CleanupOldDownloads(string folderPath, string searchPattern)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        foreach (var filePath in Directory.GetFiles(folderPath, searchPattern))
        {
            TryResetDownloadTarget(filePath);
        }
    }

    private static string GetCurrentExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return Environment.ProcessPath!;
        }

        var processName = Assembly.GetEntryAssembly()?.GetName().Name ?? "CoupleFinance.Desktop";
        return Path.Combine(AppContext.BaseDirectory, $"{processName}.exe");
    }

    private static string GetPreferredInstallDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Couple Finance");

    private bool CanApplyBackgroundPackage() =>
        _optionsMonitor.CurrentValue.PreferBackgroundPackage &&
        !string.IsNullOrWhiteSpace(_portableSource) &&
        CanWriteToDirectory(AppContext.BaseDirectory);

    private static bool CanWriteToDirectory(string directoryPath)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);
            var probePath = Path.Combine(directoryPath, $".update-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "ok", Encoding.UTF8);
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> ValidateExpectedHashAsync(string filePath, string? expectedHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return true;
        }

        return await ValidateSha256Async(filePath, expectedHash, cancellationToken);
    }

    private static async Task<bool> ValidateSha256Async(string filePath, string expectedHash, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static bool TryParseVersion(string? versionText, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return false;
        }

        var normalized = versionText.Split('-', '+')[0].Trim();
        var success = Version.TryParse(normalized, out var parsedVersion);
        version = success && parsedVersion is not null
            ? parsedVersion
            : new Version(0, 0, 0);
        return success;
    }

    private static string GetCurrentVersionText()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
        return NormalizeVersionText(version);
    }

    private static string NormalizeVersionText(Version version)
    {
        if (version.Build >= 0)
        {
            return version.ToString(3);
        }

        if (version.Minor >= 0)
        {
            return version.ToString(2);
        }

        return version.ToString();
    }

    private void NotifySummaryCardChanged()
    {
        OnPropertyChanged(nameof(LatestVersionCardTitle));
        OnPropertyChanged(nameof(LatestVersionCardValue));
        OnPropertyChanged(nameof(LatestVersionCardHint));
    }
}
