using booking_api.Data;
using booking_api.DTOs;
using booking_api.Services;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Endpoints;

public static class GameEndpoints
{
    public static WebApplication MapGameEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/games").WithTags("Games");

        group.MapGet("/", async (AppDbContext db) =>
        {
            var games = await db.Games
                .OrderBy(g => g.Name)
                .Select(g => new GameDto(g.Id, g.Name, g.Description, g.IconUrl))
                .ToListAsync();
            return Results.Ok(games);
        })
        .AllowAnonymous();

        group.MapGet("/{id:guid}/rooms", async (Guid id, AppDbContext db) =>
        {
            var rooms = await db.Rooms
                .Where(r => r.GameId == id)
                .OrderBy(r => r.Name)
                .Select(r => new RoomDto(r.Id, r.GameId, r.Name, r.Description, r.Capacity, r.HourlyRate))
                .ToListAsync();
            return Results.Ok(rooms);
        })
        .AllowAnonymous();

        group.MapGet("/{id:guid}/availability", async (Guid id, DateTime? from, int? days, IAvailabilityService svc) =>
        {
            try
            {
                var fromUtc = from?.ToUniversalTime() ?? DateTime.UtcNow.Date;
                var response = await svc.GetAsync(id, fromUtc, days ?? 7);
                return Results.Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .AllowAnonymous();

        return app;
    }
}
