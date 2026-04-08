using System.Windows.Controls;
using CoupleFinance.Desktop.Presentation;

namespace CoupleFinance.Desktop.Views.Pages;

public partial class CardsPage : UserControl
{
    public CardsPage()
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
            CardLimitSummaryCard,
            OpenInvoiceSummaryCard,
            RecentPurchasesSummaryCard);

        ResponsivePageLayoutHelper.ApplyDualColumn(
            width,
            CardsOverviewCard,
            CardsSideColumn,
            0.59);

        ResponsivePageLayoutHelper.ApplyDualColumn(
            width,
            InvoicesCard,
            InvoicePaymentCard,
            0.60);
    }
}
