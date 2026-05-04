using booking_api.Data;
using booking_api.DTOs;
using booking_api.Models;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Services;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IS3Service _s3;
    private readonly IOpenPlayService _openPlay;
    private readonly ITrustScoreService _trust;

    public PaymentService(AppDbContext db, IS3Service s3, IOpenPlayService openPlay, ITrustScoreService trust)
    {
        _db = db;
        _s3 = s3;
        _openPlay = openPlay;
        _trust = trust;
    }

    public async Task<PaymentDto> SubmitProofAsync(Guid bookingId, Guid userId, Stream proofStream, string contentType, string? referenceNumber, CancellationToken ct = default)
    {
        var booking = await _db.Bookings
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw new KeyNotFoundException("Booking not found.");

        if (booking.BookedByUserId != userId)
            throw new UnauthorizedAccessException("Not your booking.");

        if (booking.Payment is null)
            throw new InvalidOperationException("Booking has no payment record.");

        if (booking.Status != BookingStatus.PendingPayment)
            throw new InvalidOperationException($"Cannot submit proof for booking in status {booking.Status}.");

        if (booking.HoldExpiresAt is not null && booking.HoldExpiresAt < DateTime.UtcNow)
        {
            booking.Status = BookingStatus.Expired;
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Booking hold expired.");
        }

        var key = await _s3.UploadAsync(proofStream, contentType, $"payment-proofs/{booking.Id}", ct);

        booking.Payment.ProofS3Key = key;
        booking.Payment.ReferenceNumber = referenceNumber;
        booking.Payment.Status = PaymentStatus.Submitted;
        booking.Status = BookingStatus.ProofSubmitted;

        await _db.SaveChangesAsync(ct);

        return await BuildDtoAsync(booking.Payment, ct);
    }

    public async Task<IReadOnlyList<PaymentDto>> ListForReviewAsync(CancellationToken ct = default)
    {
        var pending = await _db.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.Room)
            .Include(p => p.Booking)
                .ThenInclude(b => b.BookedByUser)
            .Where(p => p.Status == PaymentStatus.Submitted)
            .OrderBy(p => p.CreationTime)
            .ToListAsync(ct);

        var dtos = new List<PaymentDto>(pending.Count);
        foreach (var p in pending)
            dtos.Add(await BuildDtoAsync(p, ct));
        return dtos;
    }

    public async Task<PaymentDto> ApproveAsync(Guid paymentId, Guid reviewerUserId, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new KeyNotFoundException("Payment not found.");

        if (payment.Status != PaymentStatus.Submitted)
            throw new InvalidOperationException($"Cannot approve a payment in status {payment.Status}.");

        payment.Status = PaymentStatus.Approved;
        payment.ReviewedByUserId = reviewerUserId;
        payment.ReviewedAt = DateTime.UtcNow;
        payment.Booking.Status = BookingStatus.Approved;
        payment.Booking.HoldExpiresAt = null;

        await _db.SaveChangesAsync(ct);

        await _trust.AdjustAsync(
            payment.Booking.BookedByUserId,
            TrustAdjustmentReason.BookingApproved,
            1f,
            "Payment approved",
            payment.BookingId,
            reviewerUserId,
            ct);

        if (payment.Booking.Type == BookingType.OpenPlaySeat)
            await _openPlay.OnSeatPaymentApprovedAsync(payment.BookingId, ct);

        return await BuildDtoAsync(payment, ct);
    }

    public async Task<PaymentDto> RejectAsync(Guid paymentId, Guid reviewerUserId, string reason, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.Room)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new KeyNotFoundException("Payment not found.");

        if (payment.Status != PaymentStatus.Submitted)
            throw new InvalidOperationException($"Cannot reject a payment in status {payment.Status}.");

        payment.Status = PaymentStatus.Rejected;
        payment.RejectionReason = reason;
        payment.ReviewedByUserId = reviewerUserId;
        payment.ReviewedAt = DateTime.UtcNow;
        payment.Booking.Status = BookingStatus.Rejected;

        await _db.SaveChangesAsync(ct);

        await _trust.AdjustAsync(
            payment.Booking.BookedByUserId,
            TrustAdjustmentReason.PaymentRejected,
            -5f,
            $"Payment rejected: {reason}",
            payment.BookingId,
            reviewerUserId,
            ct);

        return await BuildDtoAsync(payment, ct);
    }

    private async Task<PaymentDto> BuildDtoAsync(Payment p, CancellationToken ct)
    {
        string? presigned = null;
        if (!string.IsNullOrEmpty(p.ProofS3Key))
        {
            try { presigned = await _s3.GetPresignedUrlAsync(p.ProofS3Key, ct); }
            catch { presigned = null; }
        }

        await _db.Entry(p.Booking)
            .Reference(b => b.Room)
            .LoadAsync(ct);

        await _db.Entry(p.Booking)
            .Reference(b => b.BookedByUser)
            .LoadAsync(ct);

        var booker = p.Booking.BookedByUser;

        return new PaymentDto(
            p.Id, p.BookingId,
            booker != null ? $"{booker.FirstName} {booker.LastName}" : null,
            booker?.Email,
            p.Booking.Room.Name,
            p.Booking.StartTime, p.Booking.EndTime,
            p.Method, p.Status, p.Amount,
            p.ReferenceNumber, p.ProofS3Key, presigned,
            p.Remarks,
            p.RejectionReason, p.ReviewedAt
        );
    }

    public async Task<PaymentDto> SettleOutstandingAsync(Guid paymentId, PaymentMethod method, string? referenceNumber, string? remarks, Guid settledByUserId, CancellationToken ct = default)
    {
        var payment = await _db.Payments
            .Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new KeyNotFoundException("Payment not found.");

        if (payment.Status != PaymentStatus.Outstanding)
            throw new InvalidOperationException($"Cannot settle a payment in status {payment.Status}.");

        payment.Method = method;
        payment.ReferenceNumber = referenceNumber;
        payment.Remarks = remarks;
        payment.Status = PaymentStatus.Approved;
        payment.ReviewedByUserId = settledByUserId;
        payment.ReviewedAt = DateTime.UtcNow;
        payment.Booking.HoldExpiresAt = null;

        payment.Booking.BookedByUser.OutstandingBalance -= payment.Amount;
        if (payment.Booking.BookedByUser.OutstandingBalance < 0)
            payment.Booking.BookedByUser.OutstandingBalance = 0;

        await _db.SaveChangesAsync(ct);

        await _trust.AdjustAsync(
            payment.Booking.BookedByUserId,
            TrustAdjustmentReason.BookingCompleted,
            1f,
            "Outstanding payment settled",
            payment.BookingId,
            settledByUserId,
            ct);

        return await BuildDtoAsync(payment, ct);
    }
}
