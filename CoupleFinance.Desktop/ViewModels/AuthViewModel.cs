using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoupleFinance.Application.Contracts;
using CoupleFinance.Application.Models.Auth;

namespace CoupleFinance.Desktop.ViewModels;

public partial class AuthViewModel(IAuthService authService) : ObservableObject
{
    public event Func<AuthSession, Task>? Authenticated;

    [ObservableProperty] private bool isRegisterMode = true;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string displayName = string.Empty;
    [ObservableProperty] private string householdName = string.Empty;
    [ObservableProperty] private string inviteCode = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string errorMessage = string.Empty;
    [ObservableProperty] private string infoMessage = string.Empty;

    public IAsyncRelayCommand SubmitCommand => new AsyncRelayCommand(SubmitAsync, () => !IsBusy);

    public IRelayCommand ToggleModeCommand => new RelayCommand(() =>
    {
        IsRegisterMode = !IsRegisterMode;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
    });

    public void SetPassword(string value) => Password = value;

    private async Task SubmitAsync()
    {
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        IsBusy = true;

        try
        {
            AuthResult result = IsRegisterMode
                ? await authService.RegisterAsync(new RegisterRequest(
                    DisplayName,
                    Email,
                    Password,
                    string.IsNullOrWhiteSpace(HouseholdName) ? null : HouseholdName,
                    string.IsNullOrWhiteSpace(InviteCode) ? null : InviteCode))
                : await authService.SignInAsync(new LoginRequest(Email, Password));

            if (!result.Succeeded || result.Session is null)
            {
                ErrorMessage = result.ErrorMessage ?? "Não foi possível autenticar.";
                return;
            }

            if (Authenticated is not null)
            {
                await Authenticated.Invoke(result.Session);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
