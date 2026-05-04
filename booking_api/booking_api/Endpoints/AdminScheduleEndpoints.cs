using booking_api.Data;
using booking_api.DTOs;
using booking_api.Extensions;
using booking_api.Models;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Endpoints;

public static class AdminScheduleEndpoints
{
    public static WebApplication MapAdminScheduleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/schedule")
            .WithTags("Admin Schedule")
            .RequireAuthorization(p => p.RequireRole("Admin"));

        group.MapGet("/", async (AppDbContext db, DateOnly date, CancellationToken ct) =>
        {
            var dayStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var dayEnd = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            var rooms = await db.Rooms
                .Include(r => r.Game)
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.Game.Name)
                .ThenBy(r => r.Name)
                .ToListAsync(ct);

            var bookings = await db.Bookings
                .Include(b => b.BookedByUser)
                .Where(b => b.StartTime >= dayStart && b.StartTime < dayEnd.AddDays(1))
                .Select(b => new ScheduleBookingDto(
                    b.Id,
                    b.RoomId,
                    b.BookedByUser != null
                        ? $"{b.BookedByUser.FirstName} {b.BookedByUser.LastName}"
                        : "Unknown",
                    b.Type,
                    b.Status,
                    b.StartTime,
                    b.EndTime,
                    b.TotalAmount
                ))
                .ToListAsync(ct);

            var windows = await db.RoomStatusWindows
                .Where(w => w.StartTime >= dayStart && w.StartTime < dayEnd.AddDays(1))
                .Select(w => new ScheduleOpenPlayDto(
                    w.Id,
                    w.RoomId,
                    w.Status,
                    w.Notes ?? "",
                    w.MatchSize,
                    w.StartTime,
                    w.EndTime
                ))
                .ToListAsync(ct);

            return Results.Ok(new ScheduleDayDto(
                rooms.Select(r => new ScheduleRoomDto(r.Id, r.Name, new GameDto(r.Game.Id, r.Game.Name, r.Game.Description, r.Game.IconUrl))).ToList(),
                bookings,
                windows
            ));
        });

        group.MapGet("/bookings", async (AppDbContext db, DateOnly? date, string? status, Guid? roomId, CancellationToken ct) =>
        {
            var query = db.Bookings
                .Include(b => b.BookedByUser)
                .Include(b => b.Room)
                    .ThenInclude(r => r.Game)
                .AsQueryable();

            if (date.HasValue)
            {
                var dayStart = DateTime.SpecifyKind(date.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                var dayEnd = dayStart.AddDays(1);
                query = query.Where(b => b.StartTime >= dayStart && b.StartTime < dayEnd);
            }

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, true, out var parsed))
            {
                query = query.Where(b => b.Status == parsed);
            }

            if (roomId.HasValue)
            {
                query = query.Where(b => b.RoomId == roomId.Value);
            }

            var results = await query
                .OrderBy(b => b.StartTime)
                .Select(b => new AdminBookingSummaryDto(
                    b.Id,
                    b.RoomId,
                    b.BookedByUser != null
                        ? $"{b.BookedByUser.FirstName} {b.BookedByUser.LastName}"
                        : "Unknown",
                    b.Room.Name,
                    b.Type,
                    b.Status,
                    b.StartTime,
                    b.EndTime,
                    b.TotalAmount
                ))
                .ToListAsync(ct);

            return Results.Ok(results);
        });

        return app;
    }
}
