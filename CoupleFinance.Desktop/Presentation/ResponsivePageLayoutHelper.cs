using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CoupleFinance.Desktop.Presentation;

public static class ResponsivePageLayoutHelper
{
    private const double Gap = 10;
    private const double PagePadding = 4;

    public static double GetPageWidth(FrameworkElement element)
    {
        if (element is null)
        {
            return 0;
        }

        var ancestor = VisualTreeHelper.GetParent(element);
        while (ancestor is not null)
        {
            if (ancestor is ScrollViewer scrollViewer && scrollViewer.ViewportWidth > 0)
            {
                return Math.Max(320, scrollViewer.ViewportWidth - PagePadding);
            }

            ancestor = VisualTreeHelper.GetParent(ancestor);
        }

        return element.ActualWidth;
    }

    public static void ApplySummaryMetrics(double availableWidth, params FrameworkElement[] cards)
    {
        if (availableWidth <= 0 || cards.Length == 0)
        {
            return;
        }

        var preferredColumns = availableWidth >= 940 ? 4 : availableWidth >= 700 ? 3 : availableWidth >= 500 ? 2 : 1;
        var columns = Math.Min(cards.Length, preferredColumns);
        var cardWidth = (availableWidth - (Gap * (columns - 1))) / columns;
        cardWidth = Math.Max(172, cardWidth);
        var cardHeight = availableWidth <= 640 ? 112 : availableWidth <= 980 ? 118 : 124;

        for (var index = 0; index < cards.Length; index++)
        {
            cards[index].Width = cardWidth;
            cards[index].Height = cardHeight;
        }
    }

    public static void ApplyDualColumn(double availableWidth, FrameworkElement primary, FrameworkElement secondary, double primaryRatio = 0.60)
    {
        if (availableWidth <= 0)
        {
            return;
        }

        if (availableWidth >= 1020)
        {
            var usableWidth = Math.Max(620, availableWidth - Gap);
            var secondaryWidth = Math.Max(290, usableWidth * (1 - primaryRatio));
            var primaryWidth = Math.Max(390, usableWidth - secondaryWidth - Gap);
            primary.Width = primaryWidth;
            secondary.Width = secondaryWidth;
            return;
        }

        var stackedWidth = Math.Max(320, availableWidth - Gap);
        primary.Width = stackedWidth;
        secondary.Width = stackedWidth;
    }
}
