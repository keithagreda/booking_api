using booking_api.Data;
using booking_api.DTOs;
using booking_api.Extensions;
using booking_api.Models;
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
                    u.CreationTime
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
                user.CreationTime
            ));
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

        return app;
    }
}

public record RoleRequest(string Role);
