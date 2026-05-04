using booking_api.DTOs;
using booking_api.Models;

namespace booking_api.Services;

public static class RoomMapper
{
    public static async Task<RoomDto> ToDtoAsync(Room r, IS3Service s3, CancellationToken ct = default)
    {
        string? imageUrl = null;
        if (!string.IsNullOrWhiteSpace(r.ImageS3Key))
        {
            try { imageUrl = await s3.GetPresignedUrlAsync(r.ImageS3Key, ct); }
            catch { imageUrl = null; }
        }
        return new RoomDto(r.Id, r.GameId, r.Name, r.Description, r.Capacity, r.HourlyRate, imageUrl);
    }
}
