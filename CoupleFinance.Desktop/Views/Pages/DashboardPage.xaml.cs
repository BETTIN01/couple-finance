using System.Windows;
using System.Windows.Controls;

namespace CoupleFinance.Desktop.Views.Pages;

public partial class DashboardPage : UserControl
{
    private const double CardGap = 18;

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

        var metricColumns = availableWidth >= 1280 ? 4 : availableWidth >= 860 ? 2 : 1;
        var metricWidth = (availableWidth - (CardGap * metricColumns)) / metricColumns;
        metricWidth = Math.Max(250, metricWidth);

        var compactHeight = ActualHeight > 0 && ActualHeight <= 760;
        var metricHeight = compactHeight ? 148 : 164;

        IncomeMetricCard.Width = metricWidth;
        ExpenseMetricCard.Width = metricWidth;
        BalanceMetricCard.Width = metricWidth;
        InvoiceMetricCard.Width = metricWidth;

        IncomeMetricCard.Height = metricHeight;
        ExpenseMetricCard.Height = metricHeight;
        BalanceMetricCard.Height = metricHeight;
        InvoiceMetricCard.Height = metricHeight;

        var wideLayout = availableWidth >= 1180;
        if (wideLayout)
        {
            var primaryUsableWidth = Math.Max(700, availableWidth - (CardGap * 2));
            var primarySideWidth = Math.Max(360, primaryUsableWidth * 0.40);
            var primaryMainWidth = Math.Max(620, primaryUsableWidth - primarySideWidth);
            CoupleSummaryCard.Width = primaryMainWidth;
            RecentActivityCard.Width = primarySideWidth;

            var chartsUsableWidth = Math.Max(660, availableWidth - (CardGap * 2));
            var chartsSideWidth = Math.Max(340, chartsUsableWidth * 0.42);
            var chartsMainWidth = Math.Max(580, chartsUsableWidth - chartsSideWidth);
            TrendChartCard.Width = chartsMainWidth;
            CategoryChartCard.Width = chartsSideWidth;

            var footerUsableWidth = Math.Max(640, availableWidth - (CardGap * 2));
            var footerSideWidth = Math.Max(340, footerUsableWidth * 0.44);
            var footerMainWidth = Math.Max(560, footerUsableWidth - footerSideWidth);
            ComparisonCard.Width = footerMainWidth;
            PlanningColumnPanel.Width = footerSideWidth;
        }
        else
        {
            var stackedWidth = Math.Max(320, availableWidth - CardGap);
            CoupleSummaryCard.Width = stackedWidth;
            RecentActivityCard.Width = stackedWidth;
            TrendChartCard.Width = stackedWidth;
            CategoryChartCard.Width = stackedWidth;
            ComparisonCard.Width = stackedWidth;
            PlanningColumnPanel.Width = stackedWidth;
        }
    }
}
