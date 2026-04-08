using CoupleFinance.Domain.Common;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Domain.Entities;

public sealed class HouseholdMember : SyncEntity
{
    public Guid UserProfileId { get; set; }
    public HouseholdMemberRole Role { get; set; }
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
