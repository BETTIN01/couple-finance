using CoupleFinance.Application.Models.Auth;
using CoupleFinance.Application.Models.Sync;

namespace CoupleFinance.Application.Contracts;

public interface ISyncCoordinator
{
    Task<SyncResult> SyncAsync(AuthSession session, bool force = false, CancellationToken cancellationToken = default);
    Task<SyncHealth> GetHealthAsync(Guid householdId, CancellationToken cancellationToken = default);
}
