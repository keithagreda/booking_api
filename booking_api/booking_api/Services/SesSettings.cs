namespace booking_api.Services;

public class SesSettings
{
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public string FromEmail { get; set; } = string.Empty;
}
