namespace booking_api.DTOs;

public record GameDto(Guid Id, string Name, string? Description, string? IconUrl);

public record RoomDto(Guid Id, Guid GameId, string Name, string? Description, int Capacity, decimal HourlyRate, string? ImageUrl);
