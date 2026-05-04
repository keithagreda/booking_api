using booking_api.Data;
using booking_api.DTOs;
using booking_api.Models;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Services;

public class AdminCatalogService : IAdminCatalogService
{
    private readonly AppDbContext _db;
    private readonly IS3Service _s3;

    public AdminCatalogService(AppDbContext db, IS3Service s3)
    {
        _db = db;
        _s3 = s3;
    }

    // ── Games ───────────────────────────────────────────────

    public async Task<GameDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);
        var game = new Game
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            IconUrl = request.IconUrl
        };
        _db.Games.Add(game);
        await _db.SaveChangesAsync(ct);
        return ToDto(game);
    }

    public async Task<GameDto> UpdateGameAsync(Guid id, UpdateGameRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new KeyNotFoundException("Game not found.");
        game.Name = request.Name.Trim();
        game.Description = request.Description;
        game.IconUrl = request.IconUrl;
        await _db.SaveChangesAsync(ct);
        return ToDto(game);
    }

    public async Task DeleteGameAsync(Guid id, CancellationToken ct = default)
    {
        var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new KeyNotFoundException("Game not found.");
        var hasRooms = await _db.Rooms.AnyAsync(r => r.GameId == id, ct);
        if (hasRooms)
            throw new InvalidOperationException("Cannot delete a game that still has rooms. Delete or reassign its rooms first.");
        game.IsDeleted = true;
        game.DeletionTime = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── Rooms ───────────────────────────────────────────────

    public async Task<RoomDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default)
    {
        ValidateRoom(request.Name, request.Capacity, request.HourlyRate);
        var gameExists = await _db.Games.AnyAsync(g => g.Id == request.GameId, ct);
        if (!gameExists) throw new KeyNotFoundException("Game not found.");

        var room = new Room
        {
            GameId = request.GameId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Capacity = request.Capacity,
            HourlyRate = request.HourlyRate
        };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(room, ct);
    }

    public async Task<RoomDto> UpdateRoomAsync(Guid id, UpdateRoomRequest request, CancellationToken ct = default)
    {
        ValidateRoom(request.Name, request.Capacity, request.HourlyRate);
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Room not found.");
        room.Name = request.Name.Trim();
        room.Description = request.Description;
        room.Capacity = request.Capacity;
        room.HourlyRate = request.HourlyRate;
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(room, ct);
    }

    public async Task DeleteRoomAsync(Guid id, CancellationToken ct = default)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Room not found.");
        var now = DateTime.UtcNow;
        var hasFutureBookings = await _db.Bookings.AnyAsync(b =>
            b.RoomId == id
            && b.EndTime > now
            && (b.Status == BookingStatus.Approved
                || b.Status == BookingStatus.ProofSubmitted
                || b.Status == BookingStatus.PendingPayment), ct);
        if (hasFutureBookings)
            throw new InvalidOperationException("Cannot delete a room with active or upcoming bookings.");

        room.IsDeleted = true;
        room.DeletionTime = now;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<RoomDto> SetRoomImageAsync(Guid id, Stream image, string contentType, CancellationToken ct = default)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Room not found.");

        var key = await _s3.UploadAsync(image, contentType, $"room-images/{room.Id}", ct);
        room.ImageS3Key = key;
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(room, ct);
    }

    public async Task<RoomDto> RemoveRoomImageAsync(Guid id, CancellationToken ct = default)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Room not found.");

        room.ImageS3Key = null;
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(room, ct);
    }

    public async Task<IReadOnlyList<RoomDto>> ListRoomsAsync(Guid? gameId, CancellationToken ct = default)
    {
        var query = _db.Rooms.AsQueryable();
        if (gameId is not null) query = query.Where(r => r.GameId == gameId);
        var rooms = await query.OrderBy(r => r.Name).ToListAsync(ct);
        var dtos = new List<RoomDto>(rooms.Count);
        foreach (var r in rooms) dtos.Add(await ToDtoAsync(r, ct));
        return dtos;
    }

    // ── Schedule windows ────────────────────────────────────

    public async Task<IReadOnlyList<ScheduleWindowDto>> ListWindowsAsync(Guid roomId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _db.RoomStatusWindows.Where(w => w.RoomId == roomId);
        if (from is not null) query = query.Where(w => w.EndTime > from);
        if (to is not null) query = query.Where(w => w.StartTime < to);
        var windows = await query.OrderBy(w => w.StartTime).ToListAsync(ct);
        return windows.Select(ToDto).ToList();
    }

    public async Task<ScheduleWindowDto> CreateWindowAsync(Guid roomId, CreateScheduleWindowRequest request, CancellationToken ct = default)
    {
        var roomExists = await _db.Rooms.AnyAsync(r => r.Id == roomId, ct);
        if (!roomExists) throw new KeyNotFoundException("Room not found.");

        var (start, end) = NormalizeRange(request.StartTime, request.EndTime);
        ValidateOpenPlayConfig(request.Status, request.SeatRate, request.MatchSize, request.QueueCap);
        await EnsureNoOverlapAsync(roomId, start, end, excludingId: null, ct);

        var window = new RoomStatusWindow
        {
            RoomId = roomId,
            Status = request.Status,
            StartTime = start,
            EndTime = end,
            Notes = request.Notes,
            SeatRate = request.Status == RoomStatus.OpenPlay ? request.SeatRate : null,
            MatchSize = request.Status == RoomStatus.OpenPlay ? request.MatchSize : null,
            QueueCap = request.Status == RoomStatus.OpenPlay ? request.QueueCap : null
        };
        _db.RoomStatusWindows.Add(window);
        await _db.SaveChangesAsync(ct);
        return ToDto(window);
    }

    public async Task<ScheduleWindowDto> UpdateWindowAsync(Guid windowId, UpdateScheduleWindowRequest request, CancellationToken ct = default)
    {
        var window = await _db.RoomStatusWindows.FirstOrDefaultAsync(w => w.Id == windowId, ct)
            ?? throw new KeyNotFoundException("Window not found.");

        var (start, end) = NormalizeRange(request.StartTime, request.EndTime);
        ValidateOpenPlayConfig(request.Status, request.SeatRate, request.MatchSize, request.QueueCap);
        await EnsureNoOverlapAsync(window.RoomId, start, end, excludingId: window.Id, ct);

        window.Status = request.Status;
        window.StartTime = start;
        window.EndTime = end;
        window.Notes = request.Notes;
        window.SeatRate = request.Status == RoomStatus.OpenPlay ? request.SeatRate : null;
        window.MatchSize = request.Status == RoomStatus.OpenPlay ? request.MatchSize : null;
        window.QueueCap = request.Status == RoomStatus.OpenPlay ? request.QueueCap : null;
        await _db.SaveChangesAsync(ct);
        return ToDto(window);
    }

    public async Task DeleteWindowAsync(Guid windowId, CancellationToken ct = default)
    {
        var window = await _db.RoomStatusWindows.FirstOrDefaultAsync(w => w.Id == windowId, ct)
            ?? throw new KeyNotFoundException("Window not found.");

        if (window.Status == RoomStatus.OpenPlay)
        {
            var hasActivity = await _db.QueueEntries.AnyAsync(q => q.WindowId == windowId, ct)
                || await _db.Matches.AnyAsync(m => m.WindowId == windowId, ct);
            if (hasActivity)
                throw new InvalidOperationException("Cannot delete an open-play window with queue entries or matches.");
        }

        window.IsDeleted = true;
        window.DeletionTime = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ─────────────────────────────────────────────

    private async Task EnsureNoOverlapAsync(Guid roomId, DateTime start, DateTime end, Guid? excludingId, CancellationToken ct)
    {
        var overlap = await _db.RoomStatusWindows.AnyAsync(w =>
            w.RoomId == roomId
            && (excludingId == null || w.Id != excludingId)
            && w.StartTime < end && w.EndTime > start, ct);
        if (overlap)
            throw new InvalidOperationException("Window overlaps an existing window on this room.");
    }

    private static (DateTime start, DateTime end) NormalizeRange(DateTime start, DateTime end)
    {
        var s = DateTime.SpecifyKind(start.ToUniversalTime(), DateTimeKind.Utc);
        var e = DateTime.SpecifyKind(end.ToUniversalTime(), DateTimeKind.Utc);
        if (e <= s) throw new ArgumentException("End time must be after start time.");
        return (s, e);
    }

    private static void ValidateOpenPlayConfig(RoomStatus status, decimal? seatRate, int? matchSize, int? queueCap)
    {
        if (status != RoomStatus.OpenPlay) return;
        if (seatRate is null || seatRate < 0)
            throw new ArgumentException("Open-play windows require a non-negative SeatRate.");
        if (matchSize is null || matchSize < 2)
            throw new ArgumentException("Open-play windows require MatchSize ≥ 2.");
        if (queueCap is not null && queueCap < matchSize)
            throw new ArgumentException("QueueCap must be at least MatchSize.");
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.");
    }

    private static void ValidateRoom(string name, int capacity, decimal hourlyRate)
    {
        ValidateName(name);
        if (capacity <= 0) throw new ArgumentException("Capacity must be positive.");
        if (hourlyRate < 0) throw new ArgumentException("HourlyRate must be non-negative.");
    }

    private static GameDto ToDto(Game g) => new(g.Id, g.Name, g.Description, g.IconUrl);
    private Task<RoomDto> ToDtoAsync(Room r, CancellationToken ct = default) => RoomMapper.ToDtoAsync(r, _s3, ct);
    private static ScheduleWindowDto ToDto(RoomStatusWindow w) =>
        new(w.Id, w.RoomId, w.Status, w.StartTime, w.EndTime, w.Notes, w.SeatRate, w.MatchSize, w.QueueCap);
}
