using booking_api.Models;

namespace booking_api.DTOs;

public record AuditLogDto(
    Guid Id,
    AuditLevel Level,
    string? UserId,
    string? UserName,
    string? Email,
    string HttpMethod,
    string RequestUrl,
    string? IpAddress,
    string? UserAgent,
    int StatusCode,
    double DurationMs,
    DateTime CreationTime,
    string? ErrorMessage,
    string? ErrorStackTrace,
    string? RequestBody
);
