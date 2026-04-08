using CoupleFinance.Domain.Common;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Domain.Entities;

public sealed class Transaction : SyncEntity
{
    public string Description { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public TransactionKind Kind { get; set; } = TransactionKind.Expense;
    public decimal Amount { get; set; }
    public DateTime OccurredOn { get; set; } = DateTime.Today;
    public Guid? CategoryId { get; set; }
    public Guid? BankAccountId { get; set; }
    public Guid? CreditCardId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? TransferId { get; set; }
    public Guid OwnerUserId { get; set; }
    public EntryScope Scope { get; set; } = EntryScope.Individual;

    public decimal GetSignedAmount()
    {
        return Kind switch
        {
            TransactionKind.Expense or TransactionKind.TransferOut or TransactionKind.CardInvoicePayment => -Math.Abs(Amount),
            _ => Math.Abs(Amount)
        };
    }
}
