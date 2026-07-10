using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.Rooms;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Application.Rooms;

public class RoomMediaService : IRoomMediaService
{
    private readonly IAppDbContext context;
    private readonly RoomAccessService roomAccessService;
    private readonly IRoomQueryService roomQueryService;

    public RoomMediaService(
        IAppDbContext context,
        RoomAccessService roomAccessService,
        IRoomQueryService roomQueryService)
    {
        this.context = context;
        this.roomAccessService = roomAccessService;
        this.roomQueryService = roomQueryService;
    }

    public async Task<RoomResponse?> UpdateAmenitiesAsync(
        Guid landlordUserId,
        Guid roomId,
        UpdateAmenitiesRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var room = await roomAccessService.GetOwnedRoomForUpdateAsync(
                landlordUserId,
                roomId,
                cancellationToken);

            if (room is null)
            {
                return null;
            }

            roomAccessService.EnsureRoomingHouseApproved(room.RoomingHouse);

            var amenityIds = await roomAccessService.ValidateRoomAmenityIdsAsync(
                request.AmenityIds,
                cancellationToken);

            var currentAmenities = await context.RoomAmenities
                .Where(x => x.RoomId == roomId)
                .ToListAsync(cancellationToken);

            context.RoomAmenities.RemoveRange(currentAmenities);

            foreach (var amenityId in amenityIds)
            {
                context.RoomAmenities.Add(new RoomAmenity
                {
                    RoomId = roomId,
                    AmenityId = amenityId
                });
            }

            room.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return await roomQueryService.GetByIdAsync(landlordUserId, roomId, cancellationToken);
    }

    public async Task<RoomResponse?> UpdateImagesAsync(
        Guid landlordUserId,
        Guid roomId,
        UpdatePropertyImagesRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var room = await roomAccessService.GetOwnedRoomForUpdateAsync(
                landlordUserId,
                roomId,
                cancellationToken);

            if (room is null)
            {
                return null;
            }

            roomAccessService.EnsureRoomingHouseApproved(room.RoomingHouse);
            RoomValidationRules.ValidatePropertyImages(request.Images);

            var requestImageIds = request.Images
                .Where(x => x.Id.HasValue)
                .Select(x => x.Id!.Value)
                .ToHashSet();

            if (requestImageIds.Count != request.Images.Count(x => x.Id.HasValue))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Không được gửi trùng mã ảnh.",
                    new { field = nameof(request.Images) });
            }

            var currentImages = await context.PropertyImages
                .Where(x => x.RoomId == roomId)
                .ToListAsync(cancellationToken);

            var currentImageIds = currentImages.Select(x => x.Id).ToHashSet();
            var invalidImageIds = requestImageIds.Where(id => !currentImageIds.Contains(id)).ToList();

            if (invalidImageIds.Count > 0)
            {
                throw new BadRequestException(
                    ErrorCodes.ImageInvalidOwner,
                    "Một hoặc nhiều mã ảnh không hợp lệ.",
                    new { imageIds = invalidImageIds });
            }

            var imagesToDelete = currentImages
                .Where(x => !requestImageIds.Contains(x.Id))
                .ToList();

            context.PropertyImages.RemoveRange(imagesToDelete);

            foreach (var imageRequest in request.Images)
            {
                var now = DateTimeOffset.UtcNow;
                var objectKey = imageRequest.ObjectKey.Trim();

                if (imageRequest.Id.HasValue)
                {
                    var existingImage = currentImages.First(x => x.Id == imageRequest.Id.Value);
                    existingImage.ObjectKey = objectKey;
                    existingImage.ImageUrl = RoomReadModelMapper.BuildImageUrl(objectKey);
                    existingImage.Caption = imageRequest.Caption;
                    existingImage.IsCover = imageRequest.IsCover;
                    existingImage.SortOrder = imageRequest.SortOrder;
                    existingImage.MediaAssetId = await EnsurePropertyImageMediaAssetAsync(
                        existingImage.Id,
                        room.RoomingHouse.LandlordUserId,
                        objectKey,
                        existingImage.MediaAssetId,
                        MediaScope.RoomImage,
                        now,
                        cancellationToken);
                }
                else
                {
                    var propertyImage = new PropertyImage
                    {
                        Id = Guid.NewGuid(),
                        RoomId = roomId,
                        ObjectKey = objectKey,
                        ImageUrl = RoomReadModelMapper.BuildImageUrl(objectKey),
                        Caption = imageRequest.Caption,
                        IsCover = imageRequest.IsCover,
                        SortOrder = imageRequest.SortOrder,
                        CreatedAt = now
                    };
                    propertyImage.MediaAssetId = await EnsurePropertyImageMediaAssetAsync(
                        propertyImage.Id,
                        room.RoomingHouse.LandlordUserId,
                        objectKey,
                        propertyImage.MediaAssetId,
                        MediaScope.RoomImage,
                        now,
                        cancellationToken);
                    context.PropertyImages.Add(propertyImage);
                }
            }

            await UnlinkPropertyImageMediaAssetsAsync(imagesToDelete, DateTimeOffset.UtcNow, cancellationToken);

            room.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return await roomQueryService.GetByIdAsync(landlordUserId, roomId, cancellationToken);
    }

    private async Task<Guid> EnsurePropertyImageMediaAssetAsync(
        Guid propertyImageId,
        Guid ownerUserId,
        string objectKey,
        Guid? existingMediaAssetId,
        MediaScope scope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedObjectKey = NormalizeObjectKey(objectKey);

        if (existingMediaAssetId.HasValue)
        {
            var currentLinkedAsset = await context.MediaAssets
                .FirstOrDefaultAsync(x => x.Id == existingMediaAssetId.Value, cancellationToken);

            if (currentLinkedAsset is not null &&
                !string.Equals(currentLinkedAsset.ObjectKey, normalizedObjectKey, StringComparison.Ordinal))
            {
                currentLinkedAsset.LinkedEntityType = null;
                currentLinkedAsset.LinkedEntityId = null;
                currentLinkedAsset.Status = MediaStatus.Uploaded;
                currentLinkedAsset.UpdatedAt = now;
            }
        }

        var mediaAsset = await context.MediaAssets
            .FirstOrDefaultAsync(x => x.ObjectKey == normalizedObjectKey, cancellationToken);

        if (mediaAsset is null)
        {
            mediaAsset = new MediaAsset
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                BucketName = "legacy-public-storage",
                ObjectKey = normalizedObjectKey,
                OriginalFileName = Path.GetFileName(normalizedObjectKey),
                StoredFileName = Path.GetFileName(normalizedObjectKey),
                ContentType = GuessContentType(normalizedObjectKey),
                FileSize = 0,
                Scope = scope,
                Visibility = MediaVisibility.Public,
                Status = MediaStatus.Linked,
                LinkedEntityType = nameof(PropertyImage),
                LinkedEntityId = propertyImageId,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.MediaAssets.Add(mediaAsset);
            return mediaAsset.Id;
        }

        mediaAsset.OwnerUserId = ownerUserId;
        mediaAsset.Scope = scope;
        mediaAsset.Visibility = MediaVisibility.Public;
        mediaAsset.Status = MediaStatus.Linked;
        mediaAsset.LinkedEntityType = nameof(PropertyImage);
        mediaAsset.LinkedEntityId = propertyImageId;
        mediaAsset.DeletedAt = null;
        mediaAsset.UpdatedAt = now;

        return mediaAsset.Id;
    }

    private async Task UnlinkPropertyImageMediaAssetsAsync(
        IEnumerable<PropertyImage> images,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mediaAssetIds = images
            .Where(x => x.MediaAssetId.HasValue)
            .Select(x => x.MediaAssetId!.Value)
            .Distinct()
            .ToList();

        if (mediaAssetIds.Count == 0)
        {
            return;
        }

        var mediaAssets = await context.MediaAssets
            .Where(x => mediaAssetIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var mediaAsset in mediaAssets)
        {
            mediaAsset.LinkedEntityType = null;
            mediaAsset.LinkedEntityId = null;
            mediaAsset.Status = MediaStatus.Uploaded;
            mediaAsset.UpdatedAt = now;
        }
    }

    private static string NormalizeObjectKey(string objectKey)
    {
        return objectKey.Replace('\\', '/').Trim().TrimStart('/');
    }

    private static string GuessContentType(string objectKey)
    {
        return Path.GetExtension(objectKey).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}
