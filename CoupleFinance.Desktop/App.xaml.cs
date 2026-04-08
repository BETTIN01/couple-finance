using CoupleFinance.Desktop.ViewModels;
using CoupleFinance.Desktop.Configuration;
using CoupleFinance.Desktop.Services;
using CoupleFinance.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Windows;

namespace CoupleFinance.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
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

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
