using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CoupleFinance.Desktop.ViewModels;

namespace CoupleFinance.Desktop.Views.Pages;

public partial class ShellPage : UserControl
{
    private ShellViewModel? _viewModel;
    private bool _isDraggingUpdatePrompt;
    private Point _updatePromptDragStart;
    private Point _updatePromptDialogOrigin;

    public ShellPage()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyResponsiveLayout();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
        SectionScrollViewer.SizeChanged += (_, _) => ApplyResponsiveLayout();
        DataContextChanged += HandleDataContextChanged;
    }

    private void ApplyResponsiveLayout()
    {
        if (!IsLoaded)
        {
            return;
        }

        var width = ActualWidth;
        var height = ActualHeight;

        var compactWidth = width <= 1420;
        var veryCompactWidth = width <= 1260;
        var notebookWidth = width <= 1366;
        var ultraCompactWidth = width <= 1180;
        var shortHeight = height <= 820;
        var notebookHeight = height <= 800;
        var veryShortHeight = height <= 720;
        SidebarColumn.Width = new GridLength(ultraCompactWidth ? 184 : notebookWidth ? 198 : veryCompactWidth ? 216 : compactWidth ? 232 : 264);
        SidebarHost.Padding = ultraCompactWidth
            ? new Thickness(12, 14, 12, 12)
            : notebookWidth
                ? new Thickness(14, 16, 14, 14)
                : veryCompactWidth
                    ? new Thickness(16, 18, 14, 14)
                    : compactWidth
                        ? new Thickness(18, 20, 16, 16)
                        : new Thickness(20, 22, 18, 18);

        MainContentHost.Margin = ultraCompactWidth
            ? new Thickness(10, 40, 10, 14)
            : notebookWidth
                ? new Thickness(12, 42, 12, 16)
                : veryCompactWidth
                    ? new Thickness(14, 44, 14, 18)
                    : compactWidth
                        ? new Thickness(16, 46, 16, 20)
                        : new Thickness(20, 50, 20, 24);

        BrandTitleText.FontSize = ultraCompactWidth ? 18 : notebookWidth ? 20 : veryCompactWidth ? 22 : compactWidth ? 24 : 26;
        SectionTitleText.FontSize = ultraCompactWidth ? 28 : notebookWidth ? 30 : veryCompactWidth ? 32 : compactWidth ? 34 : 38;
        ShellHeaderBar.Width = ultraCompactWidth ? 58 : notebookWidth ? 72 : veryCompactWidth ? 82 : compactWidth ? 92 : 104;

        PaletteRow.Visibility = notebookHeight ? Visibility.Collapsed : Visibility.Visible;
        SidebarBrandSubtitle.Visibility = notebookHeight ? Visibility.Collapsed : Visibility.Visible;

        SidebarNavigationPanel.Margin = shortHeight
            ? new Thickness(0, 12, 0, 8)
            : new Thickness(0, 16, 0, 10);

        AccountCard.Margin = ultraCompactWidth
            ? new Thickness(0, 2, 0, 0)
            : notebookHeight
                ? new Thickness(0, 4, 0, 0)
                : new Thickness(0, 6, 0, 0);
        AccountCard.Padding = ultraCompactWidth ? new Thickness(12) : notebookWidth ? new Thickness(14) : new Thickness(16);

        UpdateStatusChip.MaxWidth = ultraCompactWidth ? 300 : notebookWidth ? 340 : veryCompactWidth ? 380 : compactWidth ? 440 : 520;
        UpdateStatusChip.Margin = new Thickness(0, 12, 0, 0);

        var sectionScale = ultraCompactWidth || veryShortHeight
            ? 0.88
            : notebookWidth || notebookHeight
                ? 0.94
                : 1.0;

        HeaderLayoutGrid.LayoutTransform = new ScaleTransform(sectionScale, sectionScale);
        SectionContentHost.LayoutTransform = new ScaleTransform(sectionScale, sectionScale);

        if (SectionScrollViewer.ViewportWidth > 0)
        {
            var scaledWidth = Math.Max(320, (SectionScrollViewer.ViewportWidth - 4) / sectionScale);
            HeaderLayoutGrid.Width = scaledWidth;
            SectionContentHost.Width = scaledWidth;
        }

        ClampUpdatePromptPosition();
    }

    private void HandleDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as ShellViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        }
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.CurrentSection))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                SectionContentHost.InvalidateMeasure();
                SectionContentHost.UpdateLayout();
                ApplyResponsiveLayout();
                SectionScrollViewer.ScrollToVerticalOffset(0);
                SectionScrollViewer.UpdateLayout();
            }));

            return;
        }

        if (e.PropertyName != nameof(ShellViewModel.IsUpdatePromptVisible))
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ResetUpdatePromptPosition));
    }

    private void UpdatePromptDragHandle_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel?.IsUpdatePromptVisible != true)
        {
            return;
        }

        _isDraggingUpdatePrompt = true;
        _updatePromptDragStart = e.GetPosition(MainContentHost);
        _updatePromptDialogOrigin = new Point(UpdatePromptDialogTransform.X, UpdatePromptDialogTransform.Y);
        UpdatePromptDragHandle.CaptureMouse();
        Mouse.OverrideCursor = Cursors.Hand;
        e.Handled = true;
    }

    private void UpdatePromptDragHandle_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingUpdatePrompt)
        {
            return;
        }

        var currentPosition = e.GetPosition(MainContentHost);
        var delta = currentPosition - _updatePromptDragStart;
        UpdatePromptDialogTransform.X = _updatePromptDialogOrigin.X + delta.X;
        UpdatePromptDialogTransform.Y = _updatePromptDialogOrigin.Y + delta.Y;
        ClampUpdatePromptPosition();
    }

    private void UpdatePromptDragHandle_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndUpdatePromptDrag();
        e.Handled = true;
    }

    private void UpdatePromptDragHandle_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        _isDraggingUpdatePrompt = false;
        Mouse.OverrideCursor = null;
    }

    private void EndUpdatePromptDrag()
    {
        if (!_isDraggingUpdatePrompt)
        {
            return;
        }

        _isDraggingUpdatePrompt = false;
        UpdatePromptDragHandle.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
    }

    private void ResetUpdatePromptPosition()
    {
        EndUpdatePromptDrag();
        UpdatePromptDialogTransform.X = 0;
        UpdatePromptDialogTransform.Y = 0;
    }

    private void ClampUpdatePromptPosition()
    {
        if (UpdatePromptDialog.ActualWidth <= 0 || UpdatePromptDialog.ActualHeight <= 0)
        {
            return;
        }

        var availableWidth = Math.Max(0, MainContentHost.ActualWidth - 32);
        var availableHeight = Math.Max(0, MainContentHost.ActualHeight - 32);
        var horizontalLimit = Math.Max(0, (availableWidth - UpdatePromptDialog.ActualWidth) / 2);
        var verticalLimit = Math.Max(0, (availableHeight - UpdatePromptDialog.ActualHeight) / 2);

        UpdatePromptDialogTransform.X = Math.Clamp(UpdatePromptDialogTransform.X, -horizontalLimit, horizontalLimit);
        UpdatePromptDialogTransform.Y = Math.Clamp(UpdatePromptDialogTransform.Y, -verticalLimit, verticalLimit);
    }
}
