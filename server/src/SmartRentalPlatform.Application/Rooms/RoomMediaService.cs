using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Media;
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
    private sealed record LinkedMediaAssetResolution(Guid MediaAssetId);

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
                var existingImage = imageRequest.Id.HasValue
                    ? currentImages.First(x => x.Id == imageRequest.Id.Value)
                    : null;
                var propertyImageId = existingImage?.Id ?? Guid.NewGuid();
                var linkedAsset = await EnsurePropertyImageMediaAssetAsync(
                    propertyImageId,
                    room.RoomingHouse.LandlordUserId,
                    imageRequest.MediaAssetId,
                    existingImage?.MediaAssetId,
                    MediaScope.RoomImage,
                    now,
                    cancellationToken);

                if (existingImage is not null)
                {
                    existingImage.ImageUrl = PublicMediaPathBuilder.Build(linkedAsset.MediaAssetId);
                    existingImage.Caption = imageRequest.Caption;
                    existingImage.IsCover = imageRequest.IsCover;
                    existingImage.SortOrder = imageRequest.SortOrder;
                    existingImage.MediaAssetId = linkedAsset.MediaAssetId;
                }
                else
                {
                    var propertyImage = new PropertyImage
                    {
                        Id = propertyImageId,
                        RoomId = roomId,
                        ImageUrl = PublicMediaPathBuilder.Build(linkedAsset.MediaAssetId),
                        Caption = imageRequest.Caption,
                        IsCover = imageRequest.IsCover,
                        SortOrder = imageRequest.SortOrder,
                        CreatedAt = now
                    };
                    propertyImage.MediaAssetId = linkedAsset.MediaAssetId;
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

    private async Task<LinkedMediaAssetResolution> EnsurePropertyImageMediaAssetAsync(
        Guid propertyImageId,
        Guid ownerUserId,
        Guid? requestedMediaAssetId,
        Guid? existingMediaAssetId,
        MediaScope scope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!requestedMediaAssetId.HasValue)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Mỗi ảnh phòng phải gửi mediaAssetId.",
                new { field = nameof(requestedMediaAssetId) });
        }

        var mediaAsset = await context.MediaAssets
            .FirstOrDefaultAsync(x => x.Id == requestedMediaAssetId.Value, cancellationToken);

        if (mediaAsset is null)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset được chọn không tồn tại.",
                new { mediaAssetId = requestedMediaAssetId.Value });
        }

        EnsurePropertyImageAssetIsReusable(mediaAsset, ownerUserId, scope);

        if (existingMediaAssetId.HasValue && existingMediaAssetId.Value != mediaAsset.Id)
        {
            var currentLinkedAsset = await context.MediaAssets
                .FirstOrDefaultAsync(x => x.Id == existingMediaAssetId.Value, cancellationToken);

            if (currentLinkedAsset is not null)
            {
                RetireMediaAsset(currentLinkedAsset, now);
            }
        }

        mediaAsset.OwnerUserId = ownerUserId;
        mediaAsset.Scope = scope;
        mediaAsset.Visibility = MediaVisibility.Public;
        mediaAsset.Status = MediaStatus.Linked;
        mediaAsset.LinkedEntityType = nameof(PropertyImage);
        mediaAsset.LinkedEntityId = propertyImageId;
        mediaAsset.DeletedAt = null;
        mediaAsset.UpdatedAt = now;

        return new LinkedMediaAssetResolution(mediaAsset.Id);
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
            RetireMediaAsset(mediaAsset, now);
        }
    }

    private static void RetireMediaAsset(MediaAsset mediaAsset, DateTimeOffset now)
    {
        mediaAsset.LinkedEntityType = null;
        mediaAsset.LinkedEntityId = null;
        mediaAsset.Status = MediaStatus.Deleted;
        mediaAsset.DeletedAt = now;
        mediaAsset.UpdatedAt = now;
    }

    private static void EnsurePropertyImageAssetIsReusable(
        MediaAsset mediaAsset,
        Guid ownerUserId,
        MediaScope expectedScope)
    {
        if (mediaAsset.OwnerUserId.HasValue && mediaAsset.OwnerUserId.Value != ownerUserId)
        {
            throw new BadRequestException(
                ErrorCodes.ImageInvalidOwner,
                "Bạn không có quyền sử dụng media asset ảnh này.",
                new { mediaAssetId = mediaAsset.Id });
        }

        if (mediaAsset.Scope != expectedScope || mediaAsset.Visibility != MediaVisibility.Public)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset không phù hợp với loại ảnh được yêu cầu.",
                new { mediaAssetId = mediaAsset.Id, expectedScope = expectedScope.ToString() });
        }

        if (mediaAsset.Status is MediaStatus.PendingUpload or MediaStatus.Deleted)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset ảnh chưa sẵn sàng để liên kết.",
                new { mediaAssetId = mediaAsset.Id, status = mediaAsset.Status.ToString() });
        }
    }

}
