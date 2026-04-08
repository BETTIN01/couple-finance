using CoupleFinance.Domain.Common;

namespace CoupleFinance.Domain.Entities;

public sealed class Installment : SyncEntity
{
    public Guid CardPurchaseId { get; set; }
    public Guid CreditCardId { get; set; }
    public Guid InvoiceId { get; set; }
    public int Number { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsPaid { get; set; }
}
