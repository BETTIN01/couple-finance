using CoupleFinance.Domain.Common;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Domain.Entities;

public sealed class Category : SyncEntity
{
    public string Name { get; set; } = string.Empty;
    public string IconKey { get; set; } = "Tag";
    public string ColorHex { get; set; } = "#FFB45E";
    public CategoryType Type { get; set; } = CategoryType.Expense;
    public bool IsDefault { get; set; }
}
