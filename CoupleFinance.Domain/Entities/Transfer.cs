using CoupleFinance.Domain.Common;

namespace CoupleFinance.Domain.Entities;

public sealed class Transfer : SyncEntity
{
    public Guid FromBankAccountId { get; set; }
    public Guid ToBankAccountId { get; set; }
    public Guid OwnerUserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredOn { get; set; } = DateTime.Today;
    public string Description { get; set; } = string.Empty;
    public Guid? DebitTransactionId { get; set; }
    public Guid? CreditTransactionId { get; set; }
}
