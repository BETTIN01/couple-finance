using System.Windows;
using System.Windows.Controls;

namespace CoupleFinance.Desktop.Views.Pages;

public partial class ShellPage : UserControl
{
    public ShellPage()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyResponsiveLayout();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
        SectionScrollViewer.SizeChanged += (_, _) => ApplyResponsiveLayout();
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
        var shortHeight = height <= 820;
        var veryShortHeight = height <= 740;

        SidebarColumn.Width = new GridLength(veryCompactWidth ? 216 : compactWidth ? 232 : 264);
        SidebarHost.Padding = veryCompactWidth
            ? new Thickness(16, 18, 14, 14)
            : compactWidth
                ? new Thickness(18, 20, 16, 16)
                : new Thickness(20, 22, 18, 18);

        MainContentHost.Margin = veryCompactWidth
            ? new Thickness(14, 56, 14, 18)
            : compactWidth
                ? new Thickness(16, 60, 16, 20)
                : new Thickness(20, 64, 20, 24);

        BrandTitleText.FontSize = veryCompactWidth ? 22 : compactWidth ? 24 : 26;
        HouseholdHeadlineText.FontSize = veryCompactWidth ? 20 : 24;
        SectionTitleText.FontSize = veryCompactWidth ? 34 : compactWidth ? 36 : 40;
        ShellHeaderBar.Width = veryCompactWidth ? 82 : compactWidth ? 92 : 104;

        PeriodCombo.Width = veryCompactWidth ? 140 : compactWidth ? 156 : 168;
        OwnershipCombo.Width = veryCompactWidth ? 188 : compactWidth ? 208 : 224;
        HeaderActionsPanel.ItemHeight = veryCompactWidth ? 44 : 48;
        HeaderFiltersCard.MaxWidth = Math.Max(420, width - SidebarColumn.Width.Value - (veryCompactWidth ? 64 : 88));

        PaletteRow.Visibility = veryShortHeight ? Visibility.Collapsed : Visibility.Visible;
        SidebarBrandSubtitle.Visibility = veryShortHeight ? Visibility.Collapsed : Visibility.Visible;

        HouseholdHeroCard.Margin = shortHeight
            ? new Thickness(0, 14, 0, 12)
            : new Thickness(0, 18, 0, 16);

        SidebarNavigationPanel.Margin = shortHeight
            ? new Thickness(0, 0, 0, 8)
            : new Thickness(0, 0, 0, 10);

        AppStatusChip.MaxWidth = veryCompactWidth ? 520 : compactWidth ? 620 : 760;
        UpdateStatusChip.MaxWidth = veryCompactWidth ? 360 : 440;

        if (SectionScrollViewer.ViewportWidth > 0)
        {
            SectionContentHost.Width = Math.Max(320, SectionScrollViewer.ViewportWidth - 4);
        }
    }
}
