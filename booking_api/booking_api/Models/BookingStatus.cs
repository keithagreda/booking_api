namespace booking_api.Models;

public enum BookingStatus
{
    PendingPayment,
    ProofSubmitted,
    Approved,
    Rejected,
    Expired,
    Cancelled
}
