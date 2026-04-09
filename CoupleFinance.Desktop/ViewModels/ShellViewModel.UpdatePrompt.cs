using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoupleFinance.Desktop.ViewModels;

public partial class ShellViewModel
{
    [ObservableProperty] private bool isUpdatePromptVisible;
    [ObservableProperty] private string updatePromptTitle = "Atualizacao pronta";
    [ObservableProperty] private string updatePromptMessage = string.Empty;

    public IRelayCommand DismissUpdatePromptCommand => new RelayCommand(HidePreparedUpdatePrompt);
    public IAsyncRelayCommand ApplyPreparedUpdatePromptCommand => new AsyncRelayCommand(ApplyPreparedUpdatePromptAsync);

    public void ShowPreparedUpdatePrompt()
    {
        if (!Updater.IsUpdateReadyToApply || string.IsNullOrWhiteSpace(Updater.PreparedVersion))
        {
            return;
        }

        UpdatePromptTitle = "Atualizacao pronta";
        UpdatePromptMessage = $"A atualizacao {Updater.PreparedVersion} ja foi baixada. Podemos reiniciar o aplicativo agora para aplicar a nova versao?";
        IsUpdatePromptVisible = true;
    }

    private void HidePreparedUpdatePrompt()
    {
        if (Updater.IsUpdateReadyToApply && !string.IsNullOrWhiteSpace(Updater.PreparedVersion))
        {
            StatusMessage = $"Atualizacao {Updater.PreparedVersion} baixada. Podemos reiniciar o app quando voce quiser aplicar.";
        }

        IsUpdatePromptVisible = false;
    }

    private async Task ApplyPreparedUpdatePromptAsync()
    {
        HidePreparedUpdatePrompt();

        var started = await Updater.ApplyPreparedUpdateAsync();
        StatusMessage = Updater.StatusText;

        if (started)
        {
            return;
        }

        StatusMessage = string.IsNullOrWhiteSpace(Updater.StatusText)
            ? "Nao foi possivel aplicar a atualizacao agora."
            : Updater.StatusText;

        ShowPreparedUpdatePrompt();
    }
}
