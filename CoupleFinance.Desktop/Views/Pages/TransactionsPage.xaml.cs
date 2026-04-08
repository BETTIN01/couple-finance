using System.Windows.Controls;
using CoupleFinance.Desktop.Presentation;

namespace CoupleFinance.Desktop.Views.Pages;

public partial class TransactionsPage : UserControl
{
    public TransactionsPage()
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
            IncomeVisibleCard,
            ExpenseVisibleCard,
            TransactionCountCard);

        ResponsivePageLayoutHelper.ApplyDualColumn(
            width,
            NewTransactionCard,
            TransferCard,
            0.61);
    }
}
