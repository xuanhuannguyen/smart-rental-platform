using SmartRentalPlatform.Application.Common.Models.Media;

namespace SmartRentalPlatform.Application.Common.Interfaces.Media;

public interface IMediaStorageService
{
    Task<MediaStoredObjectResult> UploadAsync(
        MediaUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<MediaObjectMetadataResult> GetObjectMetadataAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    string GetBucketName();

    Task<MediaUploadUrlResult> GetUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<string> GetDownloadUrlAsync(
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}
