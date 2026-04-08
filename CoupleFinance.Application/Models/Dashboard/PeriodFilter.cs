namespace CoupleFinance.Application.Models.Dashboard;

public sealed record PeriodFilter(
    PeriodPreset Preset,
    DateTime AnchorDate,
    OwnershipFilter Ownership)
{
    public DateTime StartDate =>
        Preset switch
        {
            PeriodPreset.Day => AnchorDate.Date,
            PeriodPreset.Year => new DateTime(AnchorDate.Year, 1, 1),
            _ => new DateTime(AnchorDate.Year, AnchorDate.Month, 1)
        };

    public DateTime EndDate =>
        Preset switch
        {
            PeriodPreset.Day => AnchorDate.Date.AddDays(1).AddTicks(-1),
            PeriodPreset.Year => new DateTime(AnchorDate.Year, 12, 31, 23, 59, 59),
            _ => new DateTime(AnchorDate.Year, AnchorDate.Month, DateTime.DaysInMonth(AnchorDate.Year, AnchorDate.Month), 23, 59, 59)
        };
}
