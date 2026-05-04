using booking_api.Data;
using booking_api.DTOs;
using booking_api.Models;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Services;

public class BookingService : IBookingService
{
    private readonly AppDbContext _db;
    private readonly IS3Service _s3;
    private readonly ITrustScoreService _trust;
    private const int HoldMinutes = 15;

    public BookingService(AppDbContext db, IS3Service s3, ITrustScoreService trust)
    {
        _db = db;
        _s3 = s3;
        _trust = trust;
    }

    public async Task<BookingDto> CreateRegularAsync(Guid userId, CreateRegularBookingRequest request, CancellationToken ct = default)
    {
        if (request.Hours < 1)
            throw new ArgumentException("Hours must be at least 1.");

        var start = DateTime.SpecifyKind(
            new DateTime(request.StartTime.Year, request.StartTime.Month, request.StartTime.Day, request.StartTime.Hour, 0, 0),
            DateTimeKind.Utc);
        var end = start.AddHours(request.Hours);

        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == request.RoomId, ct)
            ?? throw new KeyNotFoundException("Room not found.");

        var blockingWindow = await _db.RoomStatusWindows
            .Where(w => w.RoomId == room.Id && w.StartTime < end && w.EndTime > start)
            .FirstOrDefaultAsync(ct);

        if (blockingWindow != null && blockingWindow.Status != RoomStatus.Open)
            throw new InvalidOperationException($"Room is {blockingWindow.Status} during the requested time.");

        var now = DateTime.UtcNow;

        var conflict = await _db.Bookings.AnyAsync(b =>
            b.RoomId == room.Id
            && b.Type == BookingType.Regular
            && b.StartTime < end && b.EndTime > start
            && (b.Status == BookingStatus.Approved
                || b.Status == BookingStatus.ProofSubmitted
                || (b.Status == BookingStatus.PendingPayment && b.HoldExpiresAt != null && b.HoldExpiresAt > now)),
            ct);

        if (conflict)
            throw new InvalidOperationException("Slot is no longer available.");

        var amount = room.HourlyRate * request.Hours;

        var booking = new Booking
        {
            RoomId = room.Id,
            BookedByUserId = userId,
            StartTime = start,
            EndTime = end,
            Type = BookingType.Regular,
            Status = BookingStatus.PendingPayment,
            TotalAmount = amount,
            HoldExpiresAt = now.AddMinutes(HoldMinutes),
            Notes = request.Notes
        };

        var payment = new Payment
        {
            BookingId = booking.Id,
            Method = PaymentMethod.GCash,
            Status = PaymentStatus.AwaitingProof,
            Amount = amount
        };
        booking.Payment = payment;

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(ct);

        return await ToDtoAsync(booking, ct);
    }

    public async Task<BookingDto?> GetAsync(Guid bookingId, CancellationToken ct = default)
    {
        var booking = await _db.Bookings
            .Include(b => b.Room)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);
        return booking is null ? null : await ToDtoAsync(booking, ct);
    }

    public async Task<IReadOnlyList<BookingDto>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var bookings = await _db.Bookings
            .Include(b => b.Room)
            .Include(b => b.Payment)
            .Where(b => b.BookedByUserId == userId)
            .OrderByDescending(b => b.StartTime)
            .ToListAsync(ct);

        var dtos = new List<BookingDto>(bookings.Count);
        foreach (var b in bookings)
            dtos.Add(await ToDtoAsync(b, ct));
        return dtos;
    }

    public async Task<BookingDto> CancelAsync(Guid bookingId, Guid userId, CancellationToken ct = default)
    {
        var booking = await _db.Bookings
            .Include(b => b.Room)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw new KeyNotFoundException("Booking not found.");

        if (booking.BookedByUserId != userId)
            throw new UnauthorizedAccessException("Not your booking.");

        if (booking.Status is BookingStatus.Approved or BookingStatus.Cancelled or BookingStatus.Expired or BookingStatus.Rejected)
            throw new InvalidOperationException($"Cannot cancel a booking in status {booking.Status}.");

        booking.Status = BookingStatus.Cancelled;
        await _db.SaveChangesAsync(ct);

        await _trust.AdjustAsync(
            userId,
            TrustAdjustmentReason.BookingCancelled,
            -1f,
            "Booking cancelled by user",
            bookingId,
            ct: ct);

        return await ToDtoAsync(booking, ct);
    }

    private async Task<BookingDto> ToDtoAsync(Booking b, CancellationToken ct)
    {
        if (b.Room is null)
            await _db.Entry(b).Reference(x => x.Room).LoadAsync(ct);
        if (b.Payment is null)
            await _db.Entry(b).Reference(x => x.Payment).LoadAsync(ct);

        PaymentDto? paymentDto = null;
        if (b.Payment is not null)
        {
            string? presigned = null;
            if (!string.IsNullOrEmpty(b.Payment.ProofS3Key))
            {
                try { presigned = await _s3.GetPresignedUrlAsync(b.Payment.ProofS3Key, ct); }
                catch { presigned = null; }
            }

            var booker = b.BookedByUser;
            paymentDto = new PaymentDto(
                b.Payment.Id,
                b.Payment.BookingId,
                booker != null ? $"{booker.FirstName} {booker.LastName}" : null,
                booker?.Email,
                b.Room?.Name,
                b.StartTime, b.EndTime,
                b.Payment.Method,
                b.Payment.Status,
                b.Payment.Amount,
                b.Payment.GcashReference,
                b.Payment.ProofS3Key,
                presigned,
                b.Payment.RejectionReason,
                b.Payment.ReviewedAt
            );
        }

        return new BookingDto(
            b.Id, b.RoomId, b.Room?.Name ?? string.Empty, b.BookedByUserId,
            b.Type, b.Status, b.StartTime, b.EndTime,
            b.TotalAmount, b.HoldExpiresAt, b.Notes, paymentDto
        );
    }
}
