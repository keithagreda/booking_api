using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using booking_api.Data;
using booking_api.Models;
using Microsoft.IdentityModel.Tokens;

namespace booking_api.Middleware;

public class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly HashSet<string> _skipPaths = new()
    {
        "/hubs/live",
        "/openapi",
        "/scalar",
        "/health",
        "/favicon.ico"
    };
    private static readonly HashSet<string> _skipPrefixes = new()
    {
        "/_next",
        "/openapi/v1",
        "/api/auth/register",
        "/api/auth/login"
    };

    public AuditLogMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (_skipPaths.Contains(path) || _skipPrefixes.Any(p => path.StartsWith(p)))
        {
            await _next(context);
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Capture user info
        var userId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = context.User.FindFirstValue(ClaimTypes.Email);
        var name = context.User.FindFirstValue(ClaimTypes.Name)
            ?? context.User.Identity?.Name;

        // Capture request body (limited)
        string? requestBody = null;
        if (context.Request.Method is "POST" or "PUT" or "PATCH" && context.Request.ContentLength > 0 && context.Request.ContentLength < 4096)
        {
            try
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                if (requestBody.Length > 2000) requestBody = requestBody[..2000] + "...[truncated]";
                context.Request.Body.Position = 0;
            }
            catch
            {
                // Ignore
            }
        }

        // Capture user agent and IP
        var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();
        var ip = context.Connection.RemoteIpAddress?.ToString()
            ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? context.Request.Headers["X-Real-IP"].FirstOrDefault();

        Exception? error = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            error = ex;
            throw;
        }
        finally
        {
            sw.Stop();

            var statusCode = context.Response.StatusCode;
            var level = error != null
                ? AuditLevel.Error
                : statusCode >= 500
                    ? AuditLevel.Error
                    : statusCode >= 400
                        ? AuditLevel.Warning
                        : AuditLevel.Information;

            // Only log warnings and errors, or all requests for admin endpoints
            var isAdminEndpoint = path.StartsWith("/api/admin");
            if (level != AuditLevel.Information || isAdminEndpoint)
            {
                var audit = new AuditLog
                {
                    Level = level,
                    UserId = userId,
                    UserName = name,
                    Email = email,
                    HttpMethod = context.Request.Method,
                    RequestUrl = $"{context.Request.Path}{context.Request.QueryString}",
                    IpAddress = ip,
                    UserAgent = Truncate(userAgent, 500),
                    StatusCode = statusCode,
                    DurationMs = sw.ElapsedMilliseconds,
                    RequestBody = requestBody,
                    ErrorMessage = error?.Message,
                    ErrorStackTrace = Truncate(error?.ToString(), 4000),
                    CreationTime = DateTime.UtcNow
                };

                try
                {
                    db.AuditLogs.Add(audit);
                    await db.SaveChangesAsync();
                }
                catch
                {
                    // Don't let audit logging failures break the request
                }
            }
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (value == null) return null;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
