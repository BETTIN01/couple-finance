using CoupleFinance.Domain.Common;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Domain.Entities;

public sealed class CardPurchase : SyncEntity
{
    public Guid CreditCardId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal Amount { get; set; }
    public int InstallmentCount { get; set; } = 1;
    public DateTime PurchaseDate { get; set; } = DateTime.Today;
    public EntryScope Scope { get; set; } = EntryScope.Individual;
}
