using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.RoomingHouses;

public class RoomingHouseSubmissionService : IRoomingHouseSubmissionService
{
    private readonly IAppDbContext context;

    public RoomingHouseSubmissionService(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task<RoomingHouseDetailResponse?> SubmitAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        CancellationToken cancellationToken = default)
    {
        var roomingHouse = await context.RoomingHouses
            .Include(x => x.LegalDocument)
            .Include(x => x.Images)
            .Include(x => x.RoomingHouseAmenities)
                .ThenInclude(x => x.Amenity)
            .FirstOrDefaultAsync(
                x => x.Id == roomingHouseId &&
                     x.LandlordUserId == landlordUserId &&
                     x.DeletedAt == null,
                cancellationToken);

        if (roomingHouse is null)
        {
            return null;
        }

        if (roomingHouse.ApprovalStatus is not RoomingHouseApprovalStatus.Draft and not RoomingHouseApprovalStatus.Rejected)
        {
            throw new ConflictException(
                ErrorCodes.HouseInvalidStatus,
                "Chỉ khu trọ bản nháp hoặc bị từ chối mới có thể gửi duyệt.",
                new { currentStatus = roomingHouse.ApprovalStatus.ToString() });
        }

        await ValidateFullRoomingHouseAsync(roomingHouse, cancellationToken);

        roomingHouse.ApprovalStatus = RoomingHouseApprovalStatus.Pending;
        roomingHouse.VisibilityStatus = RoomingHouseVisibilityStatus.Hidden;
        roomingHouse.RejectedReason = null;
        roomingHouse.ReviewedByAdminId = null;
        roomingHouse.ReviewedAt = null;
        roomingHouse.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return RoomingHouseReadModelMapper.ToDetailResponse(roomingHouse);
    }

    private async Task ValidateFullRoomingHouseAsync(
        RoomingHouse roomingHouse,
        CancellationToken cancellationToken)
    {
        ValidateRoomingHouseFields(
            roomingHouse.Name,
            roomingHouse.AddressLine,
            roomingHouse.ProvinceCode,
            roomingHouse.WardCode,
            roomingHouse.Latitude,
            roomingHouse.Longitude);

        await ValidateAddressAsync(
            roomingHouse.ProvinceCode,
            roomingHouse.WardCode,
            cancellationToken);

        ValidateRequiredPropertyImages(roomingHouse.Images, "Ảnh khu trọ");

        if (roomingHouse.LegalDocument is null)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Giấy tờ pháp lý là bắt buộc trước khi gửi duyệt.",
                new { field = nameof(roomingHouse.LegalDocument) });
        }

        ValidateLegalDocumentFields(
            roomingHouse.LegalDocument.DocumentType.ToString(),
            roomingHouse.LegalDocument.FrontImageObjectKey,
            roomingHouse.LegalDocument.BackImageObjectKey,
            roomingHouse.LegalDocument.DocumentNumberMasked);
    }

    private async Task ValidateAddressAsync(
        string provinceCode,
        string wardCode,
        CancellationToken cancellationToken)
    {
        var province = provinceCode.Trim();
        var ward = wardCode.Trim();

        var provinceExists = await context.AdministrativeProvinces
            .AnyAsync(x => x.Code == province && x.IsActive, cancellationToken);

        if (!provinceExists)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Tỉnh/thành phố không hợp lệ.",
                new { field = nameof(provinceCode) });
        }

        var wardExists = await context.AdministrativeWards
            .AnyAsync(x => x.Code == ward && x.ProvinceCode == province && x.IsActive, cancellationToken);

        if (!wardExists)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Phường/xã không hợp lệ.",
                new { field = nameof(wardCode) });
        }
    }

    private static void ValidateRoomingHouseFields(
        string name,
        string addressLine,
        string provinceCode,
        string wardCode,
        decimal? latitude,
        decimal? longitude)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Tên khu trọ là bắt buộc.", new { field = nameof(name) });
        }

        if (string.IsNullOrWhiteSpace(addressLine))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Địa chỉ là bắt buộc.", new { field = nameof(addressLine) });
        }

        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Mã tỉnh/thành phố là bắt buộc.", new { field = nameof(provinceCode) });
        }

        if (string.IsNullOrWhiteSpace(wardCode))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Mã phường/xã là bắt buộc.", new { field = nameof(wardCode) });
        }

        if (latitude is < -90 or > 90)
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Vĩ độ phải nằm trong khoảng từ -90 đến 90.", new { field = nameof(latitude) });
        }

        if (longitude is < -180 or > 180)
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Kinh độ phải nằm trong khoảng từ -180 đến 180.", new { field = nameof(longitude) });
        }
    }

    private static void ValidateRequiredPropertyImages(
        IEnumerable<PropertyImage> images,
        string fieldName)
    {
        var imageList = images.ToList();

        if (imageList.Count < 3)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Khu trọ cần có ít nhất 3 ảnh trước khi gửi duyệt.",
                new { field = fieldName });
        }

        var coverCount = imageList.Count(x => x.IsCover);

        if (coverCount != 1)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Khu trọ cần có đúng 1 ảnh đại diện trước khi gửi duyệt.",
                new { field = fieldName });
        }

        if (imageList.Any(x => string.IsNullOrWhiteSpace(x.ObjectKey)))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Mã lưu trữ ảnh là bắt buộc.",
                new { field = fieldName });
        }
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

}
