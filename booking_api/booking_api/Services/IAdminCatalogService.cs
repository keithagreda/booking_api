using booking_api.DTOs;

namespace booking_api.Services;

public interface IAdminCatalogService
{
    Task<GameDto> CreateGameAsync(CreateGameRequest request, CancellationToken ct = default);
    Task<GameDto> UpdateGameAsync(Guid id, UpdateGameRequest request, CancellationToken ct = default);
    Task DeleteGameAsync(Guid id, CancellationToken ct = default);

    Task<RoomDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default);
    Task<RoomDto> UpdateRoomAsync(Guid id, UpdateRoomRequest request, CancellationToken ct = default);
    Task DeleteRoomAsync(Guid id, CancellationToken ct = default);
    Task<RoomDto> SetRoomImageAsync(Guid id, Stream image, string contentType, CancellationToken ct = default);
    Task<RoomDto> RemoveRoomImageAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<RoomDto>> ListRoomsAsync(Guid? gameId, CancellationToken ct = default);

    Task<IReadOnlyList<ScheduleWindowDto>> ListWindowsAsync(Guid roomId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<ScheduleWindowDto> CreateWindowAsync(Guid roomId, CreateScheduleWindowRequest request, CancellationToken ct = default);
    Task<ScheduleWindowDto> UpdateWindowAsync(Guid windowId, UpdateScheduleWindowRequest request, CancellationToken ct = default);
    Task DeleteWindowAsync(Guid windowId, CancellationToken ct = default);
}
