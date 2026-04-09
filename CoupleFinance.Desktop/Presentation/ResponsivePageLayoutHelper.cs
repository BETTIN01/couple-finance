using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CoupleFinance.Desktop.Presentation;

public static class ResponsivePageLayoutHelper
{
    private const double Gap = 14;
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

        var columns = availableWidth >= 1320 ? 4 : availableWidth >= 980 ? 3 : availableWidth >= 700 ? 2 : 1;
        var cardWidth = (availableWidth - (Gap * columns)) / columns;
        cardWidth = Math.Max(220, cardWidth);

        for (var index = 0; index < cards.Length; index++)
        {
            cards[index].Width = cardWidth;
        }
    }

    public static void ApplyDualColumn(double availableWidth, FrameworkElement primary, FrameworkElement secondary, double primaryRatio = 0.60)
    {
        if (availableWidth <= 0)
        {
            return;
        }

        if (availableWidth >= 1100)
        {
            var usableWidth = Math.Max(640, availableWidth - (Gap * 2));
            var secondaryWidth = Math.Max(310, usableWidth * (1 - primaryRatio));
            var primaryWidth = Math.Max(430, usableWidth - secondaryWidth);
            primary.Width = primaryWidth;
            secondary.Width = secondaryWidth;
            return;
        }

        var stackedWidth = Math.Max(320, availableWidth - Gap);
        primary.Width = stackedWidth;
        secondary.Width = stackedWidth;
    }
}
