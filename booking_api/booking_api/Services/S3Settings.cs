namespace booking_api.Services;

public class S3Settings
{
    public string Bucket { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? ServiceUrl { get; set; }
    public int PresignedUrlMinutes { get; set; } = 15;
}
