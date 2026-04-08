using CoupleFinance.Domain.Common;

namespace CoupleFinance.Domain.Entities;

public sealed class CreditCard : SyncEntity
{
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#FFF5A623";
    public decimal LimitAmount { get; set; }
    public int ClosingDay { get; set; }
    public int DueDay { get; set; }
}
