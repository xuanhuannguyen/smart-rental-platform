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
                var objectKey = imageRequest.ObjectKey.Trim();

                if (imageRequest.Id.HasValue)
                {
                    var existingImage = currentImages.First(x => x.Id == imageRequest.Id.Value);
                    existingImage.ObjectKey = objectKey;
                    existingImage.ImageUrl = BuildImageUrl(objectKey);
                    existingImage.Caption = imageRequest.Caption;
                    existingImage.IsCover = imageRequest.IsCover;
                    existingImage.SortOrder = imageRequest.SortOrder;
                }
                else
                {
                    context.PropertyImages.Add(new PropertyImage
                    {
                        Id = Guid.NewGuid(),
                        RoomingHouseId = roomingHouseId,
                        RoomId = null,
                        ObjectKey = objectKey,
                        ImageUrl = BuildImageUrl(objectKey),
                        Caption = imageRequest.Caption,
                        IsCover = imageRequest.IsCover,
                        SortOrder = imageRequest.SortOrder,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                }
            }

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
            legalDocument.FrontImageObjectKey = request.FrontImageObjectKey.Trim();
            legalDocument.BackImageObjectKey = request.BackImageObjectKey.Trim();
            legalDocument.ExtraImageObjectKey = NormalizeOptionalObjectKey(request.ExtraImageObjectKey);
            legalDocument.FrontMediaAssetId = await EnsureLegalDocumentMediaAssetAsync(
                roomingHouseId,
                roomingHouse.LandlordUserId,
                legalDocument.FrontImageObjectKey,
                legalDocument.FrontMediaAssetId,
                now,
                cancellationToken);
            legalDocument.BackMediaAssetId = await EnsureLegalDocumentMediaAssetAsync(
                roomingHouseId,
                roomingHouse.LandlordUserId,
                legalDocument.BackImageObjectKey,
                legalDocument.BackMediaAssetId,
                now,
                cancellationToken);
            legalDocument.ExtraMediaAssetId = await EnsureOptionalLegalDocumentMediaAssetAsync(
                roomingHouseId,
                roomingHouse.LandlordUserId,
                legalDocument.ExtraImageObjectKey,
                legalDocument.ExtraMediaAssetId,
                now,
                cancellationToken);

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

        if (images.Any(x => string.IsNullOrWhiteSpace(x.ObjectKey)))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Mã lưu trữ ảnh là bắt buộc.",
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
            legalDocument.FrontImageObjectKey,
            legalDocument.BackImageObjectKey,
            legalDocument.DocumentNumber);
    }

    private static LegalDocumentType ValidateLegalDocumentFields(
        string documentTypeValue,
        string frontImageObjectKey,
        string backImageObjectKey,
        string documentNumber)
    {
        if (!Enum.TryParse<LegalDocumentType>(documentTypeValue, ignoreCase: true, out var documentType))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Loại giấy tờ pháp lý không hợp lệ.",
                new { field = nameof(documentTypeValue) });
        }

        if (string.IsNullOrWhiteSpace(frontImageObjectKey))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Mã lưu trữ ảnh mặt trước giấy tờ là bắt buộc.",
                new { field = nameof(frontImageObjectKey) });
        }

        if (string.IsNullOrWhiteSpace(backImageObjectKey))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Mã lưu trữ ảnh mặt sau giấy tờ là bắt buộc.",
                new { field = nameof(backImageObjectKey) });
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

    private async Task<Guid> EnsureLegalDocumentMediaAssetAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        string objectKey,
        Guid? existingMediaAssetId,
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
                OwnerUserId = landlordUserId,
                BucketName = "legacy-uploads",
                ObjectKey = normalizedObjectKey,
                OriginalFileName = Path.GetFileName(normalizedObjectKey),
                StoredFileName = Path.GetFileName(normalizedObjectKey),
                ContentType = GuessContentType(normalizedObjectKey),
                FileSize = 0,
                Scope = MediaScope.RoomingHouseLegalDocument,
                Visibility = MediaVisibility.Private,
                Status = MediaStatus.Linked,
                LinkedEntityType = nameof(RoomingHouseLegalDocument),
                LinkedEntityId = roomingHouseId,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.MediaAssets.Add(mediaAsset);
            return mediaAsset.Id;
        }

        mediaAsset.OwnerUserId = landlordUserId;
        mediaAsset.Scope = MediaScope.RoomingHouseLegalDocument;
        mediaAsset.Visibility = MediaVisibility.Private;
        mediaAsset.Status = MediaStatus.Linked;
        mediaAsset.LinkedEntityType = nameof(RoomingHouseLegalDocument);
        mediaAsset.LinkedEntityId = roomingHouseId;
        mediaAsset.DeletedAt = null;
        mediaAsset.UpdatedAt = now;

        return mediaAsset.Id;
    }

    private async Task<Guid?> EnsureOptionalLegalDocumentMediaAssetAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        string? objectKey,
        Guid? existingMediaAssetId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
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
            objectKey,
            existingMediaAssetId,
            now,
            cancellationToken);
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

    private static string BuildImageUrl(string objectKey)
    {
        return PublicMediaPathBuilder.Build(objectKey);
    }

    private static string? NormalizeOptionalObjectKey(string? objectKey)
    {
        return string.IsNullOrWhiteSpace(objectKey) ? null : objectKey.Trim();
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
