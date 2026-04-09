using System.Windows;
using System.Windows.Controls;

namespace CoupleFinance.Desktop.Views.Pages;

public partial class DashboardPage : UserControl
{
    private const double DefaultCardGap = 12;

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyResponsiveLayout();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        if (!IsLoaded)
        {
            return;
        }

        var availableWidth = Desktop.Presentation.ResponsivePageLayoutHelper.GetPageWidth(this);
        if (availableWidth <= 0)
        {
            return;
        }

        var notebookWidth = availableWidth <= 1260;
        var compactHeight = ActualHeight > 0 && ActualHeight <= 780;
        var cardGap = notebookWidth ? 8 : DefaultCardGap;

        var metricColumns = availableWidth >= 1180 ? 4 : availableWidth >= 900 ? 3 : availableWidth >= 620 ? 2 : 1;
        var metricWidth = (availableWidth - (cardGap * (metricColumns - 1))) / metricColumns;
        metricWidth = Math.Max(186, metricWidth);

        var metricHeight = compactHeight ? 112 : notebookWidth ? 120 : 130;

        IncomeMetricCard.Width = metricWidth;
        ExpenseMetricCard.Width = metricWidth;
        BalanceMetricCard.Width = metricWidth;
        InvoiceMetricCard.Width = metricWidth;

        IncomeMetricCard.Height = metricHeight;
        ExpenseMetricCard.Height = metricHeight;
        BalanceMetricCard.Height = metricHeight;
        InvoiceMetricCard.Height = metricHeight;

        IncomeMetricCard.Margin = new Thickness(0, 0, cardGap, cardGap);
        ExpenseMetricCard.Margin = new Thickness(0, 0, cardGap, cardGap);
        BalanceMetricCard.Margin = new Thickness(0, 0, cardGap, cardGap);
        InvoiceMetricCard.Margin = new Thickness(0, 0, 0, cardGap);

        var sectionGap = notebookWidth ? 8 : DefaultCardGap;
        var wideLayout = availableWidth >= 1120;
        if (wideLayout)
        {
            var primaryUsableWidth = Math.Max(620, availableWidth - sectionGap);
            var primarySideWidth = Math.Max(300, primaryUsableWidth * 0.36);
            var primaryMainWidth = Math.Max(420, primaryUsableWidth - primarySideWidth - sectionGap);
            CoupleSummaryCard.Width = primaryMainWidth;
            RecentActivityCard.Width = primarySideWidth;

            var chartsUsableWidth = Math.Max(620, availableWidth - sectionGap);
            var chartsSideWidth = Math.Max(290, chartsUsableWidth * 0.38);
            var chartsMainWidth = Math.Max(410, chartsUsableWidth - chartsSideWidth - sectionGap);
            TrendChartCard.Width = chartsMainWidth;
            CategoryChartCard.Width = chartsSideWidth;

            var footerUsableWidth = Math.Max(620, availableWidth - sectionGap);
            var footerSideWidth = Math.Max(300, footerUsableWidth * 0.38);
            var footerMainWidth = Math.Max(400, footerUsableWidth - footerSideWidth - sectionGap);
            ComparisonCard.Width = footerMainWidth;
            PlanningColumnPanel.Width = footerSideWidth;
        }
        else
        {
            var stackedWidth = Math.Max(320, availableWidth);
            CoupleSummaryCard.Width = stackedWidth;
            RecentActivityCard.Width = stackedWidth;
            TrendChartCard.Width = stackedWidth;
            CategoryChartCard.Width = stackedWidth;
            ComparisonCard.Width = stackedWidth;
            PlanningColumnPanel.Width = stackedWidth;
        }

        CoupleSummaryCard.Margin = new Thickness(0, 0, sectionGap, sectionGap);
        RecentActivityCard.Margin = new Thickness(0, 0, 0, sectionGap);
        TrendChartCard.Margin = new Thickness(0, 0, sectionGap, sectionGap);
        CategoryChartCard.Margin = new Thickness(0, 0, 0, sectionGap);
        ComparisonCard.Margin = new Thickness(0, 0, sectionGap, 0);
    }
}
