using booking_api.Data;
using booking_api.DTOs;
using booking_api.Extensions;
using booking_api.Models;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Endpoints;

public static class AuditLogEndpoints
{
    public static WebApplication MapAuditLogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/audit-logs")
            .WithTags("Audit Logs")
            .RequireAuthorization(p => p.RequireRole("Admin"));

        group.MapGet("/", async (AppDbContext db,
            int? page,
            int? pageSize,
            AuditLevel? level,
            string? method,
            int? statusCode,
            string? search,
            DateOnly? from,
            DateOnly? to,
            CancellationToken ct) =>
        {
            var p = page ?? 1;
            var ps = pageSize ?? 50;
            if (p < 1) p = 1;
            if (ps > 200) ps = 200;

            var query = db.AuditLogs.AsQueryable();

            if (level.HasValue)
                query = query.Where(a => a.Level == level.Value);

            if (!string.IsNullOrEmpty(method))
                query = query.Where(a => a.HttpMethod == method);

            if (statusCode.HasValue)
                query = query.Where(a => a.StatusCode == statusCode.Value);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(a =>
                    a.RequestUrl.Contains(search) ||
                    (a.UserName != null && a.UserName.Contains(search)) ||
                    (a.Email != null && a.Email.Contains(search)) ||
                    (a.ErrorMessage != null && a.ErrorMessage.Contains(search)));

            if (from.HasValue)
            {
                var start = DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                query = query.Where(a => a.CreationTime >= start);
            }

            if (to.HasValue)
            {
                var end = DateTime.SpecifyKind(to.Value.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
                query = query.Where(a => a.CreationTime <= end);
            }

            var total = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(a => a.CreationTime)
                .Skip((p - 1) * ps)
                .Take(ps)
                .Select(a => new AuditLogDto(
                    a.Id,
                    a.Level,
                    a.UserId,
                    a.UserName,
                    a.Email,
                    a.HttpMethod,
                    a.RequestUrl,
                    a.IpAddress,
                    a.UserAgent,
                    a.StatusCode,
                    a.DurationMs,
                    a.CreationTime,
                    a.ErrorMessage,
                    a.ErrorStackTrace,
                    a.RequestBody
                ))
                .ToListAsync(ct);

            return Results.Ok(new { Items = items, Total = total, Page = p, PageSize = ps });
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var log = await db.AuditLogs.FindAsync([id], cancellationToken: ct);
            if (log is null) return Results.NotFound();

            return Results.Ok(new AuditLogDto(
                log.Id,
                log.Level,
                log.UserId,
                log.UserName,
                log.Email,
                log.HttpMethod,
                log.RequestUrl,
                log.IpAddress,
                log.UserAgent,
                log.StatusCode,
                log.DurationMs,
                log.CreationTime,
                log.ErrorMessage,
                log.ErrorStackTrace,
                log.RequestBody
            ));
        });

        group.MapDelete("/", async (AppDbContext db, DateOnly? before, CancellationToken ct) =>
        {
            var cutoff = before.HasValue
                ? DateTime.SpecifyKind(before.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                : DateTime.UtcNow.AddDays(-30);

            var affected = await db.AuditLogs
                .Where(a => a.CreationTime < cutoff)
                .ExecuteDeleteAsync(ct);

            return Results.Ok(new { Deleted = affected });
        });

        return app;
    }
}
