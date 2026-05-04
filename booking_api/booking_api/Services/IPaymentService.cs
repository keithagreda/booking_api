using booking_api.DTOs;
using booking_api.Models;

namespace booking_api.Services;

public interface IPaymentService
{
    Task<PaymentDto> SubmitProofAsync(Guid bookingId, Guid userId, Stream proofStream, string contentType, string? referenceNumber, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentDto>> ListForReviewAsync(CancellationToken ct = default);
    Task<PaymentDto> ApproveAsync(Guid paymentId, Guid reviewerUserId, CancellationToken ct = default);
    Task<PaymentDto> RejectAsync(Guid paymentId, Guid reviewerUserId, string reason, CancellationToken ct = default);
    Task<PaymentDto> SettleOutstandingAsync(Guid paymentId, PaymentMethod method, string? referenceNumber, string? remarks, Guid settledByUserId, CancellationToken ct = default);
}
