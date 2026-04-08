using System.Windows;
using System.Windows.Input;

namespace CoupleFinance.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
}
