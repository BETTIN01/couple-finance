using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

namespace CoupleFinance.Setup;

internal static class Program
{
    private const string AppDisplayName = "Couple Finance";
    private const string PayloadResourceName = "CoupleFinance.Payload.zip";
    private const string PayloadFileName = "CoupleFinance-portable.zip";
    private const string ExecutableName = "CoupleFinance.Desktop.exe";

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var options = InstallerOptions.Parse(args);

        try
        {
            return Install(options);
        }
        catch (Exception ex)
        {
            WriteLog(options.LogPath, ex.ToString());

            if (!options.SuppressMessageBoxes)
            {
                MessageBox.Show(
                    $"Nao foi possivel concluir a instalacao.\n\n{ex.Message}",
                    AppDisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return 1;
        }
    }

    private static int Install(InstallerOptions options)
    {
        WaitForParentProcess(options.WaitProcessId, options.LogPath);

        var installDirectory = options.InstallDirectory;
        var executablePath = Path.Combine(installDirectory, ExecutableName);
        WriteLog(options.LogPath, $"Iniciando instalacao em: {installDirectory}");

        if (!options.VerySilent && !options.SuppressStartupPrompt)
        {
            var answer = MessageBox.Show(
                $"Instalar ou atualizar o {AppDisplayName} em:\n\n{installDirectory}\n\nDeseja continuar?",
                AppDisplayName,
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);

            if (answer != DialogResult.OK)
            {
                return 1223;
            }
        }

        var workingDirectory = Path.Combine(Path.GetTempPath(), $"couplefinance-setup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var packagePath = EnsurePackagePath(workingDirectory);
            var extractDirectory = Path.Combine(workingDirectory, "payload");
            ZipFile.ExtractToDirectory(packagePath, extractDirectory, overwriteFiles: true);

            var sourceRoot = ResolveSourceRoot(extractDirectory);
            Directory.CreateDirectory(installDirectory);
            CopyDirectory(sourceRoot, installDirectory);
            ValidateInstalledVersion(executablePath, options.ExpectedVersion, options.LogPath);

            CreateProgramsShortcut(executablePath, installDirectory);
            if (!options.VerySilent || DesktopShortcutExists())
            {
                CreateDesktopShortcut(executablePath, installDirectory);
            }

            DeleteLegacyDesktopShortcut();

            if (!options.VerySilent && !options.SuppressMessageBoxes)
            {
                MessageBox.Show(
                    $"{AppDisplayName} foi instalado com sucesso.",
                    AppDisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            var shouldLaunchAfterInstall =
                File.Exists(executablePath) &&
                (options.RelaunchAfterInstall || (!options.VerySilent && !options.NoLaunch));

            if (shouldLaunchAfterInstall)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = string.IsNullOrWhiteSpace(options.ExecutablePath) ? executablePath : options.ExecutablePath,
                    WorkingDirectory = installDirectory,
                    UseShellExecute = true
                });
                WriteLog(options.LogPath, "Aplicacao reaberta apos a instalacao.");
            }

            WriteLog(options.LogPath, "Instalacao concluida com sucesso.");
            return 0;
        }
        finally
        {
            TryDeleteDirectory(workingDirectory);
        }
    }

    private static string EnsurePackagePath(string workingDirectory)
    {
        var embeddedPackagePath = Path.Combine(workingDirectory, PayloadFileName);
        using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName);
        if (resourceStream is not null)
        {
            using var destinationStream = File.Create(embeddedPackagePath);
            resourceStream.CopyTo(destinationStream);
            return embeddedPackagePath;
        }

        var adjacentPackagePath = Path.Combine(AppContext.BaseDirectory, PayloadFileName);
        if (File.Exists(adjacentPackagePath))
        {
            return adjacentPackagePath;
        }

        throw new FileNotFoundException(
            "O instalador nao encontrou o pacote interno do app. Gere o setup pela esteira de release ou mantenha o arquivo CoupleFinance-portable.zip ao lado do instalador.",
            PayloadFileName);
    }

    private static string ResolveSourceRoot(string extractDirectory)
    {
        var entries = Directory.GetDirectories(extractDirectory);
        var files = Directory.GetFiles(extractDirectory);

        if (entries.Length == 1 && files.Length == 0)
        {
            return entries[0];
        }

        return extractDirectory;
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static void CreateProgramsShortcut(string executablePath, string workingDirectory)
    {
        var programsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            AppDisplayName);

        Directory.CreateDirectory(programsDirectory);
        CreateOrUpdateShortcut(
            Path.Combine(programsDirectory, $"{AppDisplayName}.lnk"),
            executablePath,
            workingDirectory);
    }

    private static void CreateDesktopShortcut(string executablePath, string workingDirectory)
    {
        var desktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"{AppDisplayName}.lnk");

        CreateOrUpdateShortcut(desktopPath, executablePath, workingDirectory);
    }

    private static bool DesktopShortcutExists()
    {
        var userDesktopPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"{AppDisplayName}.lnk");

        return File.Exists(userDesktopPath);
    }

    private static void DeleteLegacyDesktopShortcut()
    {
        try
        {
            var commonDesktopPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                $"{AppDisplayName}.lnk");

            if (File.Exists(commonDesktopPath))
            {
                File.Delete(commonDesktopPath);
            }
        }
        catch
        {
        }
    }

    private static void CreateOrUpdateShortcut(string shortcutPath, string targetPath, string workingDirectory)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell nao esta disponivel."))!;

        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.Description = AppDisplayName;
        shortcut.IconLocation = targetPath;
        shortcut.Save();
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static void WaitForParentProcess(int processId, string logPath)
    {
        if (processId <= 0)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            WriteLog(logPath, $"Aguardando encerramento do processo pai {processId}.");
            process.WaitForExit(120000);
            WriteLog(logPath, $"Processo pai {processId} encerrado.");
        }
        catch (ArgumentException)
        {
            WriteLog(logPath, $"Processo pai {processId} nao encontrado. Continuando a instalacao.");
        }
        catch (Exception ex)
        {
            WriteLog(logPath, $"Falha ao aguardar o processo pai {processId}: {ex}");
        }

        Thread.Sleep(1500);
    }

    private static void ValidateInstalledVersion(string executablePath, string expectedVersion, string logPath)
    {
        if (string.IsNullOrWhiteSpace(expectedVersion) || !File.Exists(executablePath))
        {
            return;
        }

        var version = FileVersionInfo.GetVersionInfo(executablePath).FileVersion ?? string.Empty;
        WriteLog(logPath, $"Versao instalada detectada: {version}");

        if (!version.StartsWith(expectedVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"A instalacao terminou, mas a versao esperada era {expectedVersion} e a versao encontrada foi {version}.");
        }
    }

    private static void WriteLog(string logPath, string message)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private sealed record InstallerOptions(
        string InstallDirectory,
        bool VerySilent,
        bool SuppressMessageBoxes,
        bool SuppressStartupPrompt,
        bool NoLaunch,
        bool RelaunchAfterInstall,
        int WaitProcessId,
        string ExpectedVersion,
        string ExecutablePath,
        string LogPath)
    {
        public static InstallerOptions Parse(string[] args)
        {
            var installDirectory = GetDefaultInstallDirectory();
            var verySilent = false;
            var suppressMessageBoxes = false;
            var suppressStartupPrompt = false;
            var noLaunch = false;
            var relaunchAfterInstall = false;
            var waitProcessId = 0;
            var expectedVersion = string.Empty;
            var executablePath = string.Empty;
            var logPath = string.Empty;

            foreach (var arg in args)
            {
                if (arg.Equals("/VERYSILENT", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/SILENT", StringComparison.OrdinalIgnoreCase))
                {
                    verySilent = true;
                    continue;
                }

                if (arg.Equals("/SUPPRESSMSGBOXES", StringComparison.OrdinalIgnoreCase))
                {
                    suppressMessageBoxes = true;
                    continue;
                }

                if (arg.Equals("/SP-", StringComparison.OrdinalIgnoreCase))
                {
                    suppressStartupPrompt = true;
                    continue;
                }

                if (arg.Equals("/NOLAUNCH", StringComparison.OrdinalIgnoreCase))
                {
                    noLaunch = true;
                    continue;
                }

                if (arg.Equals("/RELAUNCH", StringComparison.OrdinalIgnoreCase))
                {
                    relaunchAfterInstall = true;
                    continue;
                }

                if (arg.StartsWith("/DIR=", StringComparison.OrdinalIgnoreCase))
                {
                    installDirectory = Unquote(arg[5..]);
                    continue;
                }

                if (arg.StartsWith("/WAITPID=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(Unquote(arg[9..]), out var parsedProcessId))
                {
                    waitProcessId = parsedProcessId;
                    continue;
                }

                if (arg.StartsWith("/EXPECTEDVERSION=", StringComparison.OrdinalIgnoreCase))
                {
                    expectedVersion = Unquote(arg[17..]);
                    continue;
                }

                if (arg.StartsWith("/EXECUTABLEPATH=", StringComparison.OrdinalIgnoreCase))
                {
                    executablePath = Unquote(arg[16..]);
                    continue;
                }

                if (arg.StartsWith("/LOGPATH=", StringComparison.OrdinalIgnoreCase))
                {
                    logPath = Unquote(arg[9..]);
                }
            }

            if (verySilent)
            {
                suppressMessageBoxes = true;
                suppressStartupPrompt = true;
                noLaunch = !relaunchAfterInstall;
            }

            return new InstallerOptions(
                installDirectory,
                verySilent,
                suppressMessageBoxes,
                suppressStartupPrompt,
                noLaunch,
                relaunchAfterInstall,
                waitProcessId,
                expectedVersion,
                executablePath,
                logPath);
        }

        private static string GetDefaultInstallDirectory() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                AppDisplayName);

        private static string Unquote(string value)
        {
            if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
            {
                return value[1..^1];
            }

            return value;
        }
    }
}
