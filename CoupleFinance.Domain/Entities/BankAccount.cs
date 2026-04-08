using CoupleFinance.Domain.Common;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Domain.Entities;

public sealed class BankAccount : SyncEntity
{
    public string Name { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#FF3561F2";
    public AccountType Type { get; set; } = AccountType.Checking;
    public decimal CurrentBalance { get; set; }
}
