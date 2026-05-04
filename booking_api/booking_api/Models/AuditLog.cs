namespace booking_api.Models;

public enum AuditLevel
{
    Information,
    Warning,
    Error
}

public class AuditLog : BaseEntity
{
    public AuditLevel Level { get; set; }

    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }

    public string HttpMethod { get; set; } = string.Empty;
    public string RequestUrl { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public int StatusCode { get; set; }
    public double DurationMs { get; set; }

    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStackTrace { get; set; }
}
