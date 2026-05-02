using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace booking_api.Services;

public class S3Service : IS3Service
{
    private readonly S3Settings _settings;
    private readonly IAmazonS3 _client;

    public S3Service(IOptions<S3Settings> settings)
    {
        _settings = settings.Value;
        if (string.IsNullOrWhiteSpace(_settings.Bucket) || string.IsNullOrWhiteSpace(_settings.Region))
            throw new InvalidOperationException("S3:Bucket and S3:Region must be configured.");

        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_settings.Region)
        };

        if (!string.IsNullOrWhiteSpace(_settings.ServiceUrl))
        {
            config.ServiceURL = _settings.ServiceUrl;
            config.ForcePathStyle = true;
        }

        _client = !string.IsNullOrWhiteSpace(_settings.AccessKey) && !string.IsNullOrWhiteSpace(_settings.SecretKey)
            ? new AmazonS3Client(new BasicAWSCredentials(_settings.AccessKey, _settings.SecretKey), config)
            : new AmazonS3Client(config);
    }

    public async Task<string> UploadAsync(Stream content, string contentType, string keyPrefix, CancellationToken ct = default)
    {
        var key = $"{keyPrefix.TrimEnd('/')}/{Guid.CreateVersion7()}";
        var request = new PutObjectRequest
        {
            BucketName = _settings.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            DisablePayloadSigning = true
        };
        await _client.PutObjectAsync(request, ct);
        return key;
    }

    public Task<string> GetPresignedUrlAsync(string key, CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.Bucket,
            Key = key,
            Expires = DateTime.UtcNow.AddMinutes(_settings.PresignedUrlMinutes),
            Verb = HttpVerb.GET
        };
        return _client.GetPreSignedURLAsync(request);
    }
}
