namespace booking_api.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Role,
    bool IsBanned,
    DateTime? BannedAt,
    DateTime CreationTime);
