using System.Windows.Controls;
using CoupleFinance.Desktop.Presentation;

namespace CoupleFinance.Desktop.Views.Pages;

public partial class InvestmentsPage : UserControl
{
    public InvestmentsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyResponsiveLayout();
        SizeChanged += (_, _) => ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        var width = ResponsivePageLayoutHelper.GetPageWidth(this);
        if (width <= 0)
        {
            return;
        }

        ResponsivePageLayoutHelper.ApplySummaryMetrics(
            width,
            TotalInvestedCard,
            CurrentMarketValueCard,
            InvestmentProfitCard);

        ResponsivePageLayoutHelper.ApplyDualColumn(
            width,
            InvestmentsMainColumn,
            NewInvestmentCard,
            0.61);
    }
}
