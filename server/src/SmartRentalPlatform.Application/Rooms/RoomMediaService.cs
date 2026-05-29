using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.Rooms;
using SmartRentalPlatform.Domain.Entities.Properties;

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
                var objectKey = imageRequest.ObjectKey.Trim();

                if (imageRequest.Id.HasValue)
                {
                    var existingImage = currentImages.First(x => x.Id == imageRequest.Id.Value);
                    existingImage.ObjectKey = objectKey;
                    existingImage.ImageUrl = RoomReadModelMapper.BuildImageUrl(objectKey);
                    existingImage.Caption = imageRequest.Caption;
                    existingImage.IsCover = imageRequest.IsCover;
                    existingImage.SortOrder = imageRequest.SortOrder;
                }
                else
                {
                    context.PropertyImages.Add(new PropertyImage
                    {
                        Id = Guid.NewGuid(),
                        RoomId = roomId,
                        ObjectKey = objectKey,
                        ImageUrl = RoomReadModelMapper.BuildImageUrl(objectKey),
                        Caption = imageRequest.Caption,
                        IsCover = imageRequest.IsCover,
                        SortOrder = imageRequest.SortOrder,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                }
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
}
