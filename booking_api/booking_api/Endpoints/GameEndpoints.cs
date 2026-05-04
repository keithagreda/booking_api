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

        group.MapGet("/rooms", async (AppDbContext db, IS3Service s3) =>
        {
            var rooms = await db.Rooms
                .Include(r => r.Game)
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.Game.Name)
                .ThenBy(r => r.Name)
                .ToListAsync();
            var dtos = new List<RoomDto>(rooms.Count);
            foreach (var r in rooms) dtos.Add(await RoomMapper.ToDtoAsync(r, s3));
            return Results.Ok(dtos);
        })
        .AllowAnonymous();

        group.MapGet("/{id:guid}/rooms", async (Guid id, AppDbContext db, IS3Service s3) =>
        {
            var rooms = await db.Rooms
                .Where(r => r.GameId == id)
                .OrderBy(r => r.Name)
                .ToListAsync();
            var dtos = new List<RoomDto>(rooms.Count);
            foreach (var r in rooms) dtos.Add(await RoomMapper.ToDtoAsync(r, s3));
            return Results.Ok(dtos);
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
