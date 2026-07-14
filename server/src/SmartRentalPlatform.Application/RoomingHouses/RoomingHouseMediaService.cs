using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.LegalDocuments;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomingHouses;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Media;
using System.Security.Cryptography;
using System.Text;

namespace SmartRentalPlatform.Application.RoomingHouses;

public class RoomingHouseMediaService : IRoomingHouseMediaService
{
    private sealed record LinkedMediaAssetResolution(Guid MediaAssetId);

    private readonly IAppDbContext context;
    private readonly IRoomingHouseQueryService queryService;

    public RoomingHouseMediaService(
        IAppDbContext context,
        IRoomingHouseQueryService queryService)
    {
        this.context = context;
        this.queryService = queryService;
    }

    public async Task<RoomingHouseDetailResponse?> UpdateAmenitiesAsync(
        Guid roomingHouseId,
        UpdateAmenitiesRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var roomingHouse = await context.RoomingHouses
                .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

            if (roomingHouse is null)
            {
                return null;
            }

            EnsureEditable(roomingHouse);

            var amenityIds = await ValidateHouseAmenityIdsAsync(request.AmenityIds, cancellationToken);
            var currentAmenities = await context.RoomingHouseAmenities
                .Where(x => x.RoomingHouseId == roomingHouseId)
                .ToListAsync(cancellationToken);

            context.RoomingHouseAmenities.RemoveRange(currentAmenities);
            AddAmenities(roomingHouseId, amenityIds);

            roomingHouse.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return await queryService.GetByIdAsync(roomingHouseId, cancellationToken);
    }

    public async Task<RoomingHouseDetailResponse?> UpdateImagesAsync(
        Guid roomingHouseId,
        UpdatePropertyImagesRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var roomingHouse = await context.RoomingHouses
                .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

            if (roomingHouse is null)
            {
                return null;
            }

            EnsureEditable(roomingHouse);
            ValidatePropertyImages(request.Images);

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
                .Where(x => x.RoomingHouseId == roomingHouseId)
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
                    roomingHouse.LandlordUserId,
                    imageRequest.MediaAssetId,
                    existingImage?.MediaAssetId,
                    MediaScope.RoomingHouseImage,
                    now,
                    cancellationToken);

                if (existingImage is not null)
                {
                    existingImage.ImageUrl = BuildImageUrl(linkedAsset.MediaAssetId);
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
                        RoomingHouseId = roomingHouseId,
                        RoomId = null,
                        ImageUrl = BuildImageUrl(linkedAsset.MediaAssetId),
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

            roomingHouse.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return await queryService.GetByIdAsync(roomingHouseId, cancellationToken);
    }

    public async Task<RoomingHouseDetailResponse?> UpdateLegalDocumentAsync(
        Guid roomingHouseId,
        UpdateRoomingHouseLegalDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        try
        {
            var roomingHouse = await context.RoomingHouses
                .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

            if (roomingHouse is null)
            {
                return null;
            }

            EnsureLegalDocumentEditable(roomingHouse);

            var documentType = ValidateLegalDocument(request);
            var now = DateTimeOffset.UtcNow;
            var documentNumber = request.DocumentNumber.Trim();

            var legalDocument = await context.RoomingHouseLegalDocuments
                .FirstOrDefaultAsync(x => x.RoomingHouseId == roomingHouseId, cancellationToken);

            if (legalDocument is null)
            {
                legalDocument = new RoomingHouseLegalDocument
                {
                    RoomingHouseId = roomingHouseId,
                    CreatedAt = now
                };

                context.RoomingHouseLegalDocuments.Add(legalDocument);
            }

            legalDocument.DocumentType = documentType;

            var frontAsset = await EnsureLegalDocumentMediaAssetAsync(
                roomingHouseId,
                roomingHouse.LandlordUserId,
                request.FrontMediaAssetId,
                legalDocument.FrontMediaAssetId,
                now,
                cancellationToken);
            var backAsset = await EnsureLegalDocumentMediaAssetAsync(
                roomingHouseId,
                roomingHouse.LandlordUserId,
                request.BackMediaAssetId,
                legalDocument.BackMediaAssetId,
                now,
                cancellationToken);
            var extraAsset = await EnsureOptionalLegalDocumentMediaAssetAsync(
                roomingHouseId,
                roomingHouse.LandlordUserId,
                request.ExtraMediaAssetId,
                legalDocument.ExtraMediaAssetId,
                now,
                cancellationToken);
            legalDocument.FrontMediaAssetId = frontAsset.MediaAssetId;
            legalDocument.BackMediaAssetId = backAsset.MediaAssetId;
            legalDocument.ExtraMediaAssetId = extraAsset?.MediaAssetId;

            if (!documentNumber.Contains('*') || string.IsNullOrEmpty(legalDocument.DocumentNumberMasked))
            {
                legalDocument.DocumentNumberMasked = MaskDocumentNumber(documentNumber);
                legalDocument.DocumentNumberHash = HashDocumentNumber(documentNumber);
            }

            legalDocument.UploadedAt = now;
            legalDocument.UpdatedAt = now;

            roomingHouse.UpdatedAt = now;

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return await queryService.GetByIdAsync(roomingHouseId, cancellationToken);
    }

    private async Task<List<int>> ValidateHouseAmenityIdsAsync(
        IEnumerable<int> requestedAmenityIds,
        CancellationToken cancellationToken)
    {
        var amenityIds = requestedAmenityIds.Distinct().ToList();
        var validAmenityIds = await context.Amenities
            .Where(x =>
                amenityIds.Contains(x.Id) &&
                x.IsActive &&
                (x.Scope == AmenityScope.House || x.Scope == AmenityScope.Both))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (validAmenityIds.Count != amenityIds.Count)
        {
            throw new BadRequestException(
                ErrorCodes.AmenityNotFound,
                "Một hoặc nhiều mã tiện ích không hợp lệ.",
                new { amenityIds });
        }

        return amenityIds;
    }

    private void AddAmenities(Guid roomingHouseId, IEnumerable<int> amenityIds)
    {
        foreach (var amenityId in amenityIds)
        {
            context.RoomingHouseAmenities.Add(new RoomingHouseAmenity
            {
                AmenityId = amenityId,
                RoomingHouseId = roomingHouseId
            });
        }
    }

    private static void ValidatePropertyImages(IReadOnlyCollection<UpdatePropertyImageItemRequest> images)
    {
        if (images.Count < 3)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Khu trọ cần có ít nhất 3 ảnh.",
                new { field = nameof(images) });
        }

        ValidateCoverImageCount(images.Count(x => x.IsCover));

        if (images.Any(x => !x.MediaAssetId.HasValue))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Mỗi ảnh khu trọ phải có mediaAssetId hợp lệ.",
                new { field = nameof(images) });
        }
    }

    private static void ValidateCoverImageCount(int coverCount)
    {
        if (coverCount != 1)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Khu trọ cần có đúng 1 ảnh đại diện.",
                new { field = "Ảnh" });
        }
    }

    private static LegalDocumentType ValidateLegalDocument(
        UpdateRoomingHouseLegalDocumentRequest legalDocument)
    {
        return ValidateLegalDocumentFields(
            legalDocument.DocumentType,
            legalDocument.FrontMediaAssetId,
            legalDocument.BackMediaAssetId,
            legalDocument.DocumentNumber);
    }

    private static LegalDocumentType ValidateLegalDocumentFields(
        string documentTypeValue,
        Guid? frontMediaAssetId,
        Guid? backMediaAssetId,
        string documentNumber)
    {
        if (!Enum.TryParse<LegalDocumentType>(documentTypeValue, ignoreCase: true, out var documentType))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Loại giấy tờ pháp lý không hợp lệ.",
                new { field = nameof(documentTypeValue) });
        }

        if (!frontMediaAssetId.HasValue)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Ảnh mặt trước giấy tờ phải có mediaAssetId hợp lệ.",
                new { field = nameof(frontMediaAssetId) });
        }

        if (!backMediaAssetId.HasValue)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Ảnh mặt sau giấy tờ phải có mediaAssetId hợp lệ.",
                new { field = nameof(backMediaAssetId) });
        }

        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Số giấy tờ là bắt buộc.",
                new { field = nameof(documentNumber) });
        }

        return documentType;
    }

    private static void EnsureEditable(RoomingHouse roomingHouse)
    {
        if (roomingHouse.ApprovalStatus == RoomingHouseApprovalStatus.Pending)
        {
            throw new ConflictException(
                ErrorCodes.HouseInvalidStatus,
                "Không thể cập nhật khu trọ đang chờ duyệt.",
                new { currentStatus = roomingHouse.ApprovalStatus.ToString() });
        }
    }

    private static void EnsureLegalDocumentEditable(RoomingHouse roomingHouse)
    {
        if (roomingHouse.ApprovalStatus is not RoomingHouseApprovalStatus.Draft and not RoomingHouseApprovalStatus.Rejected)
        {
            throw new ConflictException(
                ErrorCodes.HouseInvalidStatus,
                "Giấy tờ pháp lý chỉ được cập nhật khi khu trọ là bản nháp hoặc bị từ chối.",
                new { currentStatus = roomingHouse.ApprovalStatus.ToString() });
        }
    }

    private async Task<LinkedMediaAssetResolution> EnsureLegalDocumentMediaAssetAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        Guid? requestedMediaAssetId,
        Guid? existingMediaAssetId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!requestedMediaAssetId.HasValue)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Giấy tờ pháp lý phải gửi mediaAssetId hợp lệ.",
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

        EnsureLegalDocumentAssetIsReusable(mediaAsset, landlordUserId);

        if (existingMediaAssetId.HasValue && existingMediaAssetId.Value != mediaAsset.Id)
        {
            var currentLinkedAsset = await context.MediaAssets
                .FirstOrDefaultAsync(x => x.Id == existingMediaAssetId.Value, cancellationToken);

            if (currentLinkedAsset is not null)
            {
                currentLinkedAsset.LinkedEntityType = null;
                currentLinkedAsset.LinkedEntityId = null;
                currentLinkedAsset.Status = MediaStatus.Uploaded;
                currentLinkedAsset.UpdatedAt = now;
            }
        }

        mediaAsset.OwnerUserId = landlordUserId;
        mediaAsset.Scope = MediaScope.RoomingHouseLegalDocument;
        mediaAsset.Visibility = MediaVisibility.Private;
        mediaAsset.Status = MediaStatus.Linked;
        mediaAsset.LinkedEntityType = nameof(RoomingHouseLegalDocument);
        mediaAsset.LinkedEntityId = roomingHouseId;
        mediaAsset.DeletedAt = null;
        mediaAsset.UpdatedAt = now;

        return new LinkedMediaAssetResolution(mediaAsset.Id);
    }

    private async Task<LinkedMediaAssetResolution?> EnsureOptionalLegalDocumentMediaAssetAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        Guid? requestedMediaAssetId,
        Guid? existingMediaAssetId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!requestedMediaAssetId.HasValue)
        {
            if (existingMediaAssetId.HasValue)
            {
                var currentLinkedAsset = await context.MediaAssets
                    .FirstOrDefaultAsync(x => x.Id == existingMediaAssetId.Value, cancellationToken);

                if (currentLinkedAsset is not null)
                {
                    currentLinkedAsset.LinkedEntityType = null;
                    currentLinkedAsset.LinkedEntityId = null;
                    currentLinkedAsset.Status = MediaStatus.Uploaded;
                    currentLinkedAsset.UpdatedAt = now;
                }
            }

            return null;
        }

        return await EnsureLegalDocumentMediaAssetAsync(
            roomingHouseId,
            landlordUserId,
            requestedMediaAssetId,
            existingMediaAssetId,
            now,
            cancellationToken);
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
                "Mỗi ảnh khu trọ phải gửi mediaAssetId.",
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
                currentLinkedAsset.LinkedEntityType = null;
                currentLinkedAsset.LinkedEntityId = null;
                currentLinkedAsset.Status = MediaStatus.Uploaded;
                currentLinkedAsset.UpdatedAt = now;
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
            mediaAsset.LinkedEntityType = null;
            mediaAsset.LinkedEntityId = null;
            mediaAsset.Status = MediaStatus.Uploaded;
            mediaAsset.UpdatedAt = now;
        }
    }

    private static string MaskDocumentNumber(string documentNumber)
    {
        if (documentNumber.Length <= 4)
        {
            return new string('*', documentNumber.Length);
        }

        return new string('*', documentNumber.Length - 4) + documentNumber[^4..];
    }

    private static string HashDocumentNumber(string documentNumber)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(documentNumber));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildImageUrl(Guid mediaAssetId)
    {
        return PublicMediaPathBuilder.Build(mediaAssetId);
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

    private static void EnsureLegalDocumentAssetIsReusable(
        MediaAsset mediaAsset,
        Guid ownerUserId)
    {
        if (mediaAsset.OwnerUserId.HasValue && mediaAsset.OwnerUserId.Value != ownerUserId)
        {
            throw new BadRequestException(
                ErrorCodes.ImageInvalidOwner,
                "Bạn không có quyền sử dụng media asset giấy tờ này.",
                new { mediaAssetId = mediaAsset.Id });
        }

        if (mediaAsset.Scope != MediaScope.RoomingHouseLegalDocument || mediaAsset.Visibility != MediaVisibility.Private)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset không phù hợp với giấy tờ pháp lý.",
                new { mediaAssetId = mediaAsset.Id });
        }

        if (mediaAsset.Status is MediaStatus.PendingUpload or MediaStatus.Deleted)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset giấy tờ chưa sẵn sàng để liên kết.",
                new { mediaAssetId = mediaAsset.Id, status = mediaAsset.Status.ToString() });
        }
    }

}
