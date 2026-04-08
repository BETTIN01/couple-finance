using CoupleFinance.Domain.Common;

namespace CoupleFinance.Domain.Entities;

public sealed class Household : SyncEntity
{
    public Household()
    {
        HouseholdId = Id;
    }

    public string Name { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
}
