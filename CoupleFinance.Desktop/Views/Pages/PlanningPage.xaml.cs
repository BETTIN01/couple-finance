using System.Windows.Controls;
using CoupleFinance.Desktop.Presentation;

namespace CoupleFinance.Desktop.Views.Pages;

public partial class PlanningPage : UserControl
{
    public PlanningPage()
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
            AvailableToSaveCard,
            SavingsRateCard,
            GoalsCountCard);

        ResponsivePageLayoutHelper.ApplyDualColumn(
            width,
            GoalsProjectionCard,
            PlanningSideColumn,
            0.58);
    }
}
