using booking_api.DTOs;

namespace booking_api.Services;

public interface IBookingService
{
    Task<BookingDto> CreateRegularAsync(Guid userId, CreateRegularBookingRequest request, CancellationToken ct = default);
    Task<BookingDto?> GetAsync(Guid bookingId, CancellationToken ct = default);
    Task<IReadOnlyList<BookingDto>> GetMineAsync(Guid userId, CancellationToken ct = default);
    Task<BookingDto> CancelAsync(Guid bookingId, Guid userId, CancellationToken ct = default);
}
