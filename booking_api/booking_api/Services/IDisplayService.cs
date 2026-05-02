using booking_api.DTOs;

namespace booking_api.Services;

public interface IDisplayService
{
    Task<DisplaySnapshot> GetSnapshotAsync(CancellationToken ct = default);
}
