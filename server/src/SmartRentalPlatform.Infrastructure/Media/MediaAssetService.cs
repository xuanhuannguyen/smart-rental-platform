using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Application.Common.Models.Media;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Infrastructure.Media;

public class MediaAssetService : IMediaAssetService
{
    private readonly IAppDbContext _dbContext;

    public MediaAssetService(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MediaAsset> CreateAsync(
        CreateMediaAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var now = DateTimeOffset.UtcNow;
        var mediaAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = request.OwnerUserId,
            BucketName = request.BucketName.Trim(),
            ObjectKey = NormalizeObjectKey(request.ObjectKey),
            OriginalFileName = request.OriginalFileName.Trim(),
            StoredFileName = request.StoredFileName.Trim(),
            ContentType = request.ContentType.Trim(),
            FileSize = request.FileSize,
            FileHash = string.IsNullOrWhiteSpace(request.FileHash) ? null : request.FileHash.Trim(),
            Scope = request.Scope,
            Visibility = request.Visibility,
            Status = request.Status,
            LinkedEntityType = string.IsNullOrWhiteSpace(request.LinkedEntityType) ? null : request.LinkedEntityType.Trim(),
            LinkedEntityId = request.LinkedEntityId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.MediaAssets.Add(mediaAsset);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return mediaAsset;
    }

    public Task<MediaAsset?> GetByIdAsync(
        Guid mediaAssetId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.MediaAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == mediaAssetId, cancellationToken);
    }

    public Task<MediaAsset?> GetByObjectKeyAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedObjectKey = NormalizeObjectKey(objectKey);
        return _dbContext.MediaAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ObjectKey == normalizedObjectKey, cancellationToken);
    }

    public async Task MarkDeletedAsync(
        Guid mediaAssetId,
        CancellationToken cancellationToken = default)
    {
        var mediaAsset = await _dbContext.MediaAssets.FirstOrDefaultAsync(x => x.Id == mediaAssetId, cancellationToken);
        if (mediaAsset is null)
        {
            throw new KeyNotFoundException($"Media asset '{mediaAssetId}' was not found.");
        }

        var now = DateTimeOffset.UtcNow;
        mediaAsset.Status = MediaStatus.Deleted;
        mediaAsset.DeletedAt = now;
        mediaAsset.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateRequest(CreateMediaAssetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BucketName))
        {
            throw new ArgumentException("Bucket name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ObjectKey))
        {
            throw new ArgumentException("Object key is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            throw new ArgumentException("Original file name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.StoredFileName))
        {
            throw new ArgumentException("Stored file name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            throw new ArgumentException("Content type is required.", nameof(request));
        }

        if (request.FileSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "File size must be non-negative.");
        }
    }

    private static string NormalizeObjectKey(string objectKey)
    {
        return objectKey.Replace('\\', '/').Trim().TrimStart('/');
    }
}
