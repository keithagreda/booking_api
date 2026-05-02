using booking_api.DTOs;

namespace booking_api.Services;

public interface IOpenPlayService
{
    Task<JoinOpenPlayResponse> JoinAsync(Guid windowId, Guid userId, Guid? partnerUserId, CancellationToken ct = default);
    Task<OpenPlayWindowState> AcceptPartyAsync(Guid partyId, Guid userId, CancellationToken ct = default);
    Task<OpenPlayWindowState> LeaveAsync(Guid windowId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<OpenPlayWindowSummary>> ListLiveAsync(CancellationToken ct = default);
    Task<OpenPlayWindowState> GetStateAsync(Guid windowId, CancellationToken ct = default);
    Task OnSeatPaymentApprovedAsync(Guid bookingId, CancellationToken ct = default);
    Task<MatchDto> EndMatchAsync(Guid matchId, Guid adminUserId, CancellationToken ct = default);
}
