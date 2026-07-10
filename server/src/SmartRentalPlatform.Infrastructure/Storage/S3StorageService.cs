using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Options;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Infrastructure.Options;

namespace SmartRentalPlatform.Infrastructure.Storage;

public sealed class S3StorageService : IMediaStorageService, IPrivateStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3StorageOptions _options;

    public S3StorageService(
        IAmazonS3 s3Client,
        IOptions<S3StorageOptions> options)
    {
        _s3Client = s3Client;
        _options = options.Value;
    }

    public async Task<MediaStoredObjectResult> UploadAsync(
        MediaUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedObjectKey = NormalizeObjectKey(request.ObjectKey);

        var putRequest = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = normalizedObjectKey,
            InputStream = request.Content,
            ContentType = request.ContentType,
            AutoCloseStream = false,
            AutoResetStreamPosition = request.Content.CanSeek,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        };

        await _s3Client.PutObjectAsync(putRequest, cancellationToken);

        return new MediaStoredObjectResult
        {
            BucketName = _options.BucketName,
            ObjectKey = normalizedObjectKey,
            PublicUrl = IsPublicObject(normalizedObjectKey)
                ? PublicMediaPathBuilder.Build(normalizedObjectKey)
                : null,
            StoredFileName = Path.GetFileName(normalizedObjectKey)
        };
    }

    public async Task<string> UploadAsync(
        Stream content,
        string contentType,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedObjectKey = NormalizeObjectKey(objectKey);

        var putRequest = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = normalizedObjectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            AutoResetStreamPosition = content.CanSeek,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        };

        await _s3Client.PutObjectAsync(putRequest, cancellationToken);
        return normalizedObjectKey;
    }

    public async Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedObjectKey = NormalizeObjectKey(objectKey);
        var response = await _s3Client.GetObjectAsync(_options.BucketName, normalizedObjectKey, cancellationToken);
        return new S3ObjectReadStream(response);
    }

    public async Task<bool> ExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedObjectKey = NormalizeObjectKey(objectKey);

        try
        {
            await _s3Client.GetObjectMetadataAsync(_options.BucketName, normalizedObjectKey, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedObjectKey = NormalizeObjectKey(objectKey);
        await _s3Client.DeleteObjectAsync(_options.BucketName, normalizedObjectKey, cancellationToken);
    }

    public Task<string> GetDownloadUrlAsync(
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var normalizedObjectKey = NormalizeObjectKey(objectKey);

        if (IsPublicObject(normalizedObjectKey))
        {
            return Task.FromResult(PublicMediaPathBuilder.Build(normalizedObjectKey));
        }

        var url = _s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = normalizedObjectKey,
            Expires = DateTime.UtcNow.Add(ttl)
        });

        return Task.FromResult(url);
    }

    private static bool IsPublicObject(string objectKey)
    {
        return objectKey.StartsWith("public/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeObjectKey(string objectKey)
    {
        return objectKey.Replace('\\', '/').Trim().TrimStart('/');
    }

    public static AmazonS3Client CreateClient(S3StorageOptions options)
    {
        ValidateOptions(options);

        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region)
        };

        return new AmazonS3Client(options.AccessKeyId, options.SecretAccessKey, config);
    }

    public static async Task ValidateConnectivityAsync(
        IAmazonS3 s3Client,
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        var exists = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName);
        if (!exists)
        {
            throw new InvalidOperationException($"Configured S3 bucket '{bucketName}' does not exist or is not accessible.");
        }
    }

    public static void ValidateOptions(S3StorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AccessKeyId) ||
            string.IsNullOrWhiteSpace(options.SecretAccessKey) ||
            string.IsNullOrWhiteSpace(options.Region) ||
            string.IsNullOrWhiteSpace(options.BucketName))
        {
            throw new InvalidOperationException("Aws:S3 configuration is incomplete. AccessKeyId, SecretAccessKey, Region, and BucketName are required.");
        }
    }

    private sealed class S3ObjectReadStream : Stream
    {
        private readonly GetObjectResponse _response;
        private readonly Stream _innerStream;

        public S3ObjectReadStream(GetObjectResponse response)
        {
            _response = response;
            _innerStream = response.ResponseStream;
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _innerStream.Length;
        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        public override void Flush() => _innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
        public override void SetLength(long value) => _innerStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => await _innerStream.ReadAsync(buffer, cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _innerStream.ReadAsync(buffer, offset, count, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
                _response.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _innerStream.DisposeAsync();
            _response.Dispose();
            await base.DisposeAsync();
        }
    }
}
