using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CoupleFinance.Application.Contracts;
using CoupleFinance.Application.Models.Auth;
using CoupleFinance.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoupleFinance.Desktop.ViewModels;

public partial class MainViewModel(
    IAuthService authService,
    IServiceProvider serviceProvider,
    AppUpdateService appUpdateService) : ObservableObject
{
    private readonly DispatcherTimer _automaticUpdateTimer = new();
    private bool _isRunningAutomaticUpdateCycle;

    [ObservableProperty] private object? currentViewModel;

    public async Task InitializeAsync()
    {
        ConfigureAutomaticUpdateTimer();

        var restoredSession = await authService.RestoreSessionAsync();
        if (restoredSession is not null)
        {
            await ShowShellAsync(restoredSession);
            return;
        }

        ShowAuth();
    }

    public async Task RunPostStartupTasksAsync()
    {
        if (!appUpdateService.CheckOnStartup)
        {
            return;
        }

        await RunAutomaticUpdateCycleAsync(waitForStartupDelay: true);
    }

    private void ConfigureAutomaticUpdateTimer()
    {
        _automaticUpdateTimer.Stop();
        _automaticUpdateTimer.Tick -= HandleAutomaticUpdateTick;

        if (!appUpdateService.PeriodicChecksEnabled)
        {
            return;
        }

        _automaticUpdateTimer.Interval = appUpdateService.PeriodicCheckInterval;
        _automaticUpdateTimer.Tick += HandleAutomaticUpdateTick;
        _automaticUpdateTimer.Start();
    }

    private async void HandleAutomaticUpdateTick(object? sender, EventArgs e) => await RunAutomaticUpdateCycleAsync();

    private async Task RunAutomaticUpdateCycleAsync(bool waitForStartupDelay = false)
    {
        if (_isRunningAutomaticUpdateCycle)
        {
            return;
        }

        _isRunningAutomaticUpdateCycle = true;
        try
        {
            if (waitForStartupDelay && appUpdateService.StartupDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(appUpdateService.StartupDelaySeconds));
            }

            var updateAvailable = await appUpdateService.CheckForUpdatesAsync(background: true);
            if (!updateAvailable || !appUpdateService.AutoInstallEnabled)
            {
                return;
            }

            await appUpdateService.DownloadAndInstallAsync();
        }
        finally
        {
            _isRunningAutomaticUpdateCycle = false;
        }
    }

    private void ShowAuth(string? message = null)
    {
        if (CurrentViewModel is ShellViewModel previousShell)
        {
            previousShell.SignedOut -= HandleSignedOutAsync;
            previousShell.Dispose();
        }

        var authViewModel = serviceProvider.GetRequiredService<AuthViewModel>();
        authViewModel.Authenticated -= HandleAuthenticatedAsync;
        authViewModel.Authenticated += HandleAuthenticatedAsync;
        authViewModel.InfoMessage = message ?? string.Empty;
        CurrentViewModel = authViewModel;
    }

    private async Task ShowShellAsync(AuthSession session)
    {
        if (CurrentViewModel is AuthViewModel previousAuth)
        {
            previousAuth.Authenticated -= HandleAuthenticatedAsync;
        }

        if (CurrentViewModel is ShellViewModel previousShell)
        {
            previousShell.SignedOut -= HandleSignedOutAsync;
            previousShell.Dispose();
        }

        var shellViewModel = serviceProvider.GetRequiredService<ShellViewModel>();
        shellViewModel.SignedOut -= HandleSignedOutAsync;
        shellViewModel.SignedOut += HandleSignedOutAsync;
        await shellViewModel.InitializeAsync(session);
        CurrentViewModel = shellViewModel;
    }

    private async Task HandleAuthenticatedAsync(AuthSession session) => await ShowShellAsync(session);

    private async Task HandleSignedOutAsync()
    {
        await authService.SignOutAsync();
        ShowAuth("Sessão encerrada.");
    }
}
