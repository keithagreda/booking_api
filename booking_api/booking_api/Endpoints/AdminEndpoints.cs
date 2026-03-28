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

        group.MapGet("/users", async (AppDbContext db) =>
        {
            var users = await db.Users
                .Select(u => new UserDto(u.Id, u.Email!, u.FirstName, u.LastName, u.PhoneNumber, u.Role.ToString()))
                .ToListAsync();

            return Results.Ok(users);
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

        return app;
    }
}
