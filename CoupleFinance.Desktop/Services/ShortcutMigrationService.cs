using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace CoupleFinance.Desktop.Services;

public sealed class ShortcutMigrationService(ILogger<ShortcutMigrationService> logger)
{
    private const string AppDisplayName = "Couple Finance";
    private const string ExecutableName = "CoupleFinance.Desktop.exe";

    public void RepairForCurrentInstall()
    {
        try
        {
            var executablePath = GetCurrentExecutablePath();
            var workingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;

            EnsureUserProgramsShortcut(executablePath, workingDirectory);
            RepairDesktopShortcut(executablePath, workingDirectory);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Nao foi possivel reparar os atalhos da instalacao atual.");
        }
    }

    private void EnsureUserProgramsShortcut(string executablePath, string workingDirectory)
    {
        var programsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            AppDisplayName);

        Directory.CreateDirectory(programsFolder);
        var shortcutPath = Path.Combine(programsFolder, $"{AppDisplayName}.lnk");
        CreateOrUpdateShortcut(shortcutPath, executablePath, workingDirectory);
    }

    private void RepairDesktopShortcut(string executablePath, string workingDirectory)
    {
        var userShortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"{AppDisplayName}.lnk");

        var commonShortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            $"{AppDisplayName}.lnk");

        var commonShortcutTarget = TryReadShortcutTarget(commonShortcutPath);
        var hasLegacyDesktopShortcut = !string.IsNullOrWhiteSpace(commonShortcutTarget) &&
                                       !PathsEqual(commonShortcutTarget, executablePath);

        if (hasLegacyDesktopShortcut || File.Exists(userShortcutPath))
        {
            CreateOrUpdateShortcut(userShortcutPath, executablePath, workingDirectory);
        }

        if (hasLegacyDesktopShortcut)
        {
            TryDeleteShortcut(commonShortcutPath);
        }
    }

    private void CreateOrUpdateShortcut(string shortcutPath, string targetPath, string workingDirectory)
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

    private string? TryReadShortcutTarget(string shortcutPath)
    {
        if (!File.Exists(shortcutPath))
        {
            return null;
        }

        try
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell nao esta disponivel."))!;

            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            return shortcut.TargetPath as string;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Nao foi possivel ler o atalho {ShortcutPath}.", shortcutPath);
            return null;
        }
    }

    private void TryDeleteShortcut(string shortcutPath)
    {
        try
        {
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Nao foi possivel remover o atalho legado {ShortcutPath}.", shortcutPath);
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static string GetCurrentExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return Environment.ProcessPath!;
        }

        return Path.Combine(AppContext.BaseDirectory, ExecutableName);
    }
}
