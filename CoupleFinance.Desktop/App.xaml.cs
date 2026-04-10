using CoupleFinance.Desktop.ViewModels;
using CoupleFinance.Desktop.Configuration;
using CoupleFinance.Desktop.Services;
using CoupleFinance.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace CoupleFinance.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            var culture = new CultureInfo("pt-BR");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(builder =>
                {
                    builder.SetBasePath(AppContext.BaseDirectory);
                    builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddCoupleFinanceServices(context.Configuration);
                    services.Configure<UpdateOptions>(context.Configuration.GetSection("Updates"));
                    services.Configure<SyncAutomationOptions>(context.Configuration.GetSection("SyncAutomation"));
                    services.AddSingleton<AppUpdateService>();
                    services.AddSingleton<ShortcutMigrationService>();
                    services.AddTransient<AuthViewModel>();
                    services.AddTransient<ShellViewModel>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await _host.StartAsync();

            var shortcutMigrationService = _host.Services.GetRequiredService<ShortcutMigrationService>();
            shortcutMigrationService.RepairForCurrentInstall();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
            mainWindow.DataContext = mainViewModel;
            await mainViewModel.InitializeAsync();
            mainWindow.Show();
            _ = mainViewModel.RunPostStartupTasksAsync();
        }
        catch (Exception ex)
        {
            TryWriteStartupCrashLog(ex);
            MessageBox.Show(
                "O app encontrou um erro ao abrir e sera fechado. Um log foi salvo em AppData\\Local\\CoupleFinance\\Logs para diagnostico.",
                "Falha ao iniciar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static void TryWriteStartupCrashLog(Exception ex)
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CoupleFinance",
                "Logs");
            Directory.CreateDirectory(root);

            var logPath = Path.Combine(root, "startup-crash.log");
            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Falha ao iniciar o app");
            builder.AppendLine(ex.ToString());
            builder.AppendLine(new string('-', 80));

            File.AppendAllText(logPath, builder.ToString(), Encoding.UTF8);
        }
        catch
        {
            // no-op: nunca interromper o fluxo de erro por falha ao gravar log.
        }
    }
}
