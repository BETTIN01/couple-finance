using CoupleFinance.Domain.Common;
using CoupleFinance.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoupleFinance.Domain.Entities;

public sealed class Invoice : SyncEntity
{
    public Guid CreditCardId { get; set; }
    public int ReferenceMonth { get; set; }
    public int ReferenceYear { get; set; }
    public DateTime ClosingDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    public DateTime? PaidAtUtc { get; set; }

    [NotMapped]
    public string ReferenceLabel => $"{ReferenceMonth:00}/{ReferenceYear}";

    public void RegisterPayment(decimal amount, DateTime paidAtUtc)
    {
        PaidAmount += amount;
        PaidAtUtc = paidAtUtc;
        Status = PaidAmount >= TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.Pending;
        Touch();
    }
}
