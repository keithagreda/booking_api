namespace booking_api.Services;

public interface IS3Service
{
    Task<string> UploadAsync(Stream content, string contentType, string keyPrefix, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string key, CancellationToken ct = default);
}
