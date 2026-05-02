using booking_api.DTOs;

namespace booking_api.Services;

public interface IAvailabilityService
{
    Task<AvailabilityResponse> GetAsync(Guid gameId, DateTime fromUtc, int days, CancellationToken ct = default);
}
