using System.Windows.Controls;
using CoupleFinance.Desktop.Presentation;

namespace CoupleFinance.Desktop.Views.Pages;

public partial class AccountsPage : UserControl
{
    public AccountsPage()
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
            AccountBalanceCard,
            ActiveAccountsCard,
            CategoriesCountCard);

        ResponsivePageLayoutHelper.ApplyDualColumn(
            width,
            AccountsOverviewCard,
            AccountsSideColumn,
            0.59);
    }
}
