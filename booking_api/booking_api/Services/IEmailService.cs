namespace booking_api.Services;

public interface IEmailService
{
    Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken ct = default);
    Task SendPasswordChangedAsync(string email, CancellationToken ct = default);
}
