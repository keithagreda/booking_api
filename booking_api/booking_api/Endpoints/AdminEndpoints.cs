using booking_api.Data;
using booking_api.DTOs;
using booking_api.Extensions;
using booking_api.Models;
using booking_api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Endpoints;

public static class AdminEndpoints
{
    public static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapGet("/users", async (AppDbContext db,
            int? page,
            int? pageSize,
            string? search,
            string? role,
            bool? isBanned,
            CancellationToken ct) =>
        {
            var p = page ?? 1;
            var ps = pageSize ?? 50;
            if (p < 1) p = 1;
            if (ps > 200) ps = 200;

            var query = db.Users.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(u =>
                    u.Email!.Contains(search) ||
                    u.FirstName.Contains(search) ||
                    u.LastName.Contains(search) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(search)));

            if (!string.IsNullOrEmpty(role))
                query = query.Where(u => u.Role.ToString() == role);

            if (isBanned.HasValue)
                query = query.Where(u => u.IsBanned == isBanned.Value);

            var total = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(u => u.CreationTime)
                .Skip((p - 1) * ps)
                .Take(ps)
                .Select(u => new UserDto(
                    u.Id,
                    u.Email!,
                    u.FirstName,
                    u.LastName,
                    u.PhoneNumber,
                    u.Role.ToString(),
                    u.IsBanned,
                    u.BannedAt,
                    u.CreationTime,
                    u.TrustScore
                ))
                .ToListAsync(ct);

            return Results.Ok(new { Items = items, Total = total, Page = p, PageSize = ps });
        });

        group.MapGet("/users/{userId:guid}", async (Guid userId, AppDbContext db, CancellationToken ct) =>
        {
            var user = await db.Users.FindAsync([userId], cancellationToken: ct);
            if (user is null) return Results.NotFound();

            return Results.Ok(new UserDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.Role.ToString(),
                user.IsBanned,
                user.BannedAt,
                user.CreationTime,
                user.TrustScore
            ));
        });

        group.MapGet("/users/{userId:guid}/trust-history", async (Guid userId, AppDbContext db,
            int? page, int? pageSize, CancellationToken ct) =>
        {
            var p = page ?? 1;
            var ps = pageSize ?? 50;
            if (p < 1) p = 1;
            if (ps > 200) ps = 200;

            var user = await db.Users.FindAsync([userId], cancellationToken: ct);
            if (user is null) return Results.NotFound();

            var query = db.TrustScoreHistory
                .Where(t => t.UserId == userId)
                .Include(t => t.Booking)
                    .ThenInclude(b => b!.Room)
                .Include(t => t.TriggeredByUser);

            var total = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(t => t.CreationTime)
                .Skip((p - 1) * ps)
                .Take(ps)
                .Select(t => new TrustHistoryDto(
                    t.Id,
                    t.PreviousScore,
                    t.NewScore,
                    t.Adjustment,
                    t.Reason.ToString(),
                    t.Details,
                    t.Booking != null ? t.Booking.Room.Name : null,
                    t.TriggeredByUser != null ? $"{t.TriggeredByUser.FirstName} {t.TriggeredByUser.LastName}" : null,
                    t.CreationTime
                ))
                .ToListAsync(ct);

            return Results.Ok(new { Items = items, Total = total, Page = p, PageSize = ps });
        });

        group.MapPost("/users/{userId:guid}/ban", async (Guid userId, HttpContext httpContext, UserManager<User> userManager) =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Results.NotFound(new { error = "User not found." });

            if (user.IsBanned)
                return Results.BadRequest(new { error = "User is already banned." });

            user.IsBanned = true;
            user.BannedAt = DateTime.UtcNow;
            user.BannedByUserId = httpContext.User.GetUserId();
            await userManager.UpdateAsync(user);

            return Results.Ok(new { message = "User banned successfully." });
        });

        group.MapPost("/users/{userId:guid}/unban", async (Guid userId, UserManager<User> userManager) =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Results.NotFound(new { error = "User not found." });

            if (!user.IsBanned)
                return Results.BadRequest(new { error = "User is not banned." });

            user.IsBanned = false;
            user.BannedAt = null;
            user.BannedByUserId = null;
            await userManager.UpdateAsync(user);

            return Results.Ok(new { message = "User unbanned successfully." });
        });

        group.MapPost("/users/{userId:guid}/role", async (Guid userId, HttpContext httpContext, UserManager<User> userManager,
            RoleRequest request) =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Results.NotFound(new { error = "User not found." });

            if (!Enum.TryParse<Role>(request.Role, true, out var newRole))
                return Results.BadRequest(new { error = "Invalid role. Must be 'Player' or 'Admin'." });

            if (user.Role == newRole)
                return Results.BadRequest(new { error = "User already has this role." });

            var oldRole = user.Role;
            user.Role = newRole;
            user.LastModificationTime = DateTime.UtcNow;
            user.LastModifiedByUserId = httpContext.User.GetUserId();
            await userManager.UpdateAsync(user);

            return Results.Ok(new { message = $"Role changed from {oldRole} to {newRole}." });
        });

        group.MapPost("/users/{userId:guid}/impersonate", async (Guid userId, HttpContext httpContext,
            UserManager<User> userManager, IAuthTokenGenerator tokenGenerator, CancellationToken ct) =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Results.NotFound(new { error = "User not found." });

            if (user.IsBanned)
                return Results.BadRequest(new { error = "Cannot impersonate a banned user." });

            var token = tokenGenerator.GenerateToken(user);

            return Results.Ok(new { Token = token });
        });

        group.MapPost("/users/{userId:guid}/set-password", async (Guid userId, HttpContext httpContext,
            UserManager<User> userManager, IEmailService emailService, SetPasswordRequest request, CancellationToken ct) =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Results.NotFound(new { error = "User not found." });

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
            if (!result.Succeeded)
                return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            user.LastModificationTime = DateTime.UtcNow;
            user.LastModifiedByUserId = httpContext.User.GetUserId();
            await userManager.UpdateAsync(user);

            await emailService.SendPasswordChangedAsync(user.Email!, ct);

            return Results.Ok(new { message = "Password changed successfully. User was notified via email." });
        });

        group.MapPost("/users/{userId:guid}/reset-password", async (Guid userId,
            UserManager<User> userManager, IEmailService emailService,
            IConfiguration configuration, CancellationToken ct) =>
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Results.NotFound(new { error = "User not found." });

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = Uri.EscapeDataString(token);
            var encodedEmail = Uri.EscapeDataString(user.Email!);
            var frontendUrl = configuration["FrontendUrl"] ?? "http://localhost:3000";
            var resetUrl = $"{frontendUrl}/reset-password?token={encodedToken}&email={encodedEmail}";

            await emailService.SendPasswordResetAsync(user.Email!, resetUrl, ct);

            return Results.Ok(new { message = "Password reset email sent." });
        });

        group.MapPost("/users/{userId:guid}/trust", async (Guid userId, HttpContext httpContext,
            ITrustScoreService trustService, AdjustTrustRequest request, CancellationToken ct) =>
        {
            await trustService.AdjustAsync(
                userId,
                TrustAdjustmentReason.ManualAdjustment,
                request.Adjustment,
                request.Reason,
                triggeredByUserId: httpContext.User.GetUserId(),
                ct: ct);

            return Results.Ok(new { message = $"Trust score adjusted by {request.Adjustment:+0.0;-#.#;0}." });
        });

        return app;
    }
}

public record RoleRequest(string Role);
public record SetPasswordRequest(string NewPassword);
public record AdjustTrustRequest(float Adjustment, string? Reason);
public record TrustHistoryDto(
    Guid Id,
    float PreviousScore,
    float NewScore,
    float Adjustment,
    string Reason,
    string? Details,
    string? BookingRoom,
    string? TriggeredBy,
    DateTime CreationTime);
