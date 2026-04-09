using System.Windows;
using System.Windows.Input;

namespace CoupleFinance.Desktop;

public partial class MainWindow : Window
{
    private bool _startupBoundsApplied;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += Window_OnLoaded;
        UpdateWindowChromeState();
    }

    private void DragSurface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        DragMove();
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestoreButton_OnClick(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    private void Window_OnStateChanged(object sender, EventArgs e) => UpdateWindowChromeState();

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_startupBoundsApplied)
        {
            return;
        }

        ApplyAdaptiveStartupBounds();
        _startupBoundsApplied = true;
    }

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void UpdateWindowChromeState()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowFrame.BorderThickness = new Thickness(0);
            WindowFrame.CornerRadius = new CornerRadius(0);
            MaximizeGlyph.Text = "\uE923";
            return;
        }

        WindowFrame.BorderThickness = new Thickness(1);
        WindowFrame.CornerRadius = new CornerRadius(28);
        MaximizeGlyph.Text = "\uE922";
    }

    private void ApplyAdaptiveStartupBounds()
    {
        var workArea = SystemParameters.WorkArea;
        var compactScreen = workArea.Width <= 1500 || workArea.Height <= 900;

        if (compactScreen)
        {
            WindowState = WindowState.Maximized;
            return;
        }

        var targetWidth = Math.Min(1460, workArea.Width - 72);
        var targetHeight = Math.Min(940, workArea.Height - 64);

        Width = Math.Max(MinWidth, targetWidth);
        Height = Math.Max(MinHeight, targetHeight);
        Left = workArea.Left + ((workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(16, (workArea.Height - Height) / 2);
    }
}
