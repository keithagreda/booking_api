using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Options;

namespace booking_api.Services;

public class SesEmailService : IEmailService
{
    private readonly SesSettings _settings;
    private readonly ILogger<SesEmailService> _log;

    public SesEmailService(IOptions<SesSettings> options, ILogger<SesEmailService> log)
    {
        _settings = options.Value;
        _log = log;
    }

    public async Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_settings.AccessKey))
        {
            _log.LogWarning("SES not configured — skipping password reset email to {Email}", email);
            return;
        }

        var client = CreateClient();
        var request = new SendEmailRequest
        {
            Source = _settings.FromEmail,
            Destination = new Destination { ToAddresses = new List<string> { email } },
            Message = new Message
            {
                Subject = new Content("Reset your Centre Court password"),
                Body = new Body
                {
                    Html = new Content(
                        $"<p>Click the link below to reset your password:</p>" +
                        $"<p><a href=\"{resetUrl}\">Reset Password</a></p>" +
                        $"<p>If you did not request this, please ignore this email.</p>"),
                    Text = new Content(
                        $"Reset your password: {resetUrl}\n\nIf you did not request this, please ignore this email.")
                }
            }
        };

        await client.SendEmailAsync(request, ct);
        _log.LogInformation("Password reset email sent to {Email}", email);
    }

    public async Task SendPasswordChangedAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_settings.AccessKey))
        {
            _log.LogWarning("SES not configured — skipping password changed notification to {Email}", email);
            return;
        }

        var client = CreateClient();
        var request = new SendEmailRequest
        {
            Source = _settings.FromEmail,
            Destination = new Destination { ToAddresses = new List<string> { email } },
            Message = new Message
            {
                Subject = new Content("Your Centre Court password was changed"),
                Body = new Body
                {
                    Html = new Content(
                        "<p>Your password has been changed by an administrator. " +
                        "If you did not expect this, please contact support.</p>"),
                    Text = new Content(
                        "Your password has been changed by an administrator. " +
                        "If you did not expect this, please contact support.")
                }
            }
        };

        await client.SendEmailAsync(request, ct);
        _log.LogInformation("Password changed notification sent to {Email}", email);
    }

    private IAmazonSimpleEmailService CreateClient()
    {
        var config = new AmazonSimpleEmailServiceConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_settings.Region)
        };
        return new AmazonSimpleEmailServiceClient(_settings.AccessKey, _settings.SecretKey, config);
    }
}
