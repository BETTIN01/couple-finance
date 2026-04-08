using CoupleFinance.Desktop.ViewModels;
using System.Windows.Controls;

namespace CoupleFinance.Desktop.Views.Pages;

public partial class AuthPage : UserControl
{
    public AuthPage()
    {
        InitializeComponent();
    }

    private void PasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AuthViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.SetPassword(passwordBox.Password);
        }
    }
}
