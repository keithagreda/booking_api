using booking_api.DTOs;

namespace booking_api.Services;

public interface IPaymentService
{
    Task<PaymentDto> SubmitProofAsync(Guid bookingId, Guid userId, Stream proofStream, string contentType, string? gcashReference, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentDto>> ListForReviewAsync(CancellationToken ct = default);
    Task<PaymentDto> ApproveAsync(Guid paymentId, Guid reviewerUserId, CancellationToken ct = default);
    Task<PaymentDto> RejectAsync(Guid paymentId, Guid reviewerUserId, string reason, CancellationToken ct = default);
}
