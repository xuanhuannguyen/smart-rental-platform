using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RoomingHouses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.RoomingHouses;

public class RoomingHouseDraftService : IRoomingHouseDraftService
{
    private readonly IAppDbContext context;
    private readonly IRoomingHouseQueryService queryService;

    public RoomingHouseDraftService(
        IAppDbContext context,
        IRoomingHouseQueryService queryService)
    {
        this.context = context;
        this.queryService = queryService;
    }

    public async Task<RoomingHouseDetailResponse> CreateDraftAsync(
        Guid landlordUserId,
        CreateRoomingHouseDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var activeOnboardingHouse = await context.RoomingHouses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.LandlordUserId == landlordUserId &&
                     (x.ApprovalStatus == RoomingHouseApprovalStatus.Draft ||
                      x.ApprovalStatus == RoomingHouseApprovalStatus.Pending ||
                      x.ApprovalStatus == RoomingHouseApprovalStatus.Rejected) &&
                     x.DeletedAt == null,
                cancellationToken);

        if (activeOnboardingHouse is not null)
        {
            throw new ConflictException(
                ErrorCodes.HouseInvalidStatus,
                BuildCreateDraftBlockedMessage(activeOnboardingHouse.ApprovalStatus),
                new
                {
                    roomingHouseId = activeOnboardingHouse.Id,
                    currentStatus = activeOnboardingHouse.ApprovalStatus.ToString()
                });
        }

        ValidateRoomingHouseFields(request);
        var addressDisplay = await BuildAddressDisplayAsync(
            request.AddressLine,
            request.ProvinceCode,
            request.WardCode,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var roomingHouse = new RoomingHouse
        {
            Id = Guid.NewGuid(),
            LandlordUserId = landlordUserId,
            Name = request.Name.Trim(),
            Description = request.Description,
            AddressLine = request.AddressLine.Trim(),
            ProvinceCode = request.ProvinceCode.Trim(),
            WardCode = request.WardCode.Trim(),
            AddressDisplay = addressDisplay,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            GoogleMapUrl = NormalizeOptionalUrl(request.GoogleMapUrl),
            ApprovalStatus = RoomingHouseApprovalStatus.Draft,
            VisibilityStatus = RoomingHouseVisibilityStatus.Hidden,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.RoomingHouses.Add(roomingHouse);
        await context.SaveChangesAsync(cancellationToken);

        return await queryService.GetByIdAsync(roomingHouse.Id, cancellationToken)
            ?? throw new InternalServerException(
                ErrorCodes.InternalServerError,
                "Đã tạo bản nháp khu trọ nhưng không thể tải lại thông tin.",
                new { roomingHouseId = roomingHouse.Id });
    }

    public async Task<RoomingHouseDetailResponse?> UpdateAsync(
        Guid roomingHouseId,
        UpdateRoomingHouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var roomingHouse = await context.RoomingHouses
            .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

        if (roomingHouse is null)
        {
            return null;
        }

        EnsureEditable(roomingHouse);
        ValidateRoomingHouseFields(request);
        EnsureAddressEditable(roomingHouse, request);

        var addressDisplay = roomingHouse.ApprovalStatus == RoomingHouseApprovalStatus.Approved
            ? roomingHouse.AddressDisplay
            : await BuildAddressDisplayAsync(
                request.AddressLine,
                request.ProvinceCode,
                request.WardCode,
                cancellationToken);

        roomingHouse.Name = request.Name.Trim();
        roomingHouse.Description = request.Description;
        if (roomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Approved)
        {
            roomingHouse.AddressLine = request.AddressLine.Trim();
            roomingHouse.ProvinceCode = request.ProvinceCode.Trim();
            roomingHouse.WardCode = request.WardCode.Trim();
        }
        roomingHouse.AddressDisplay = addressDisplay;
        roomingHouse.Latitude = request.Latitude;
        roomingHouse.Longitude = request.Longitude;
        roomingHouse.GoogleMapUrl = NormalizeOptionalUrl(request.GoogleMapUrl);
        roomingHouse.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return await queryService.GetByIdAsync(roomingHouseId, cancellationToken);
    }

    public async Task<RoomingHouseDetailResponse?> UpdateVisibilityAsync(
        Guid roomingHouseId,
        UpdateRoomingHouseVisibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var roomingHouse = await context.RoomingHouses
            .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

        if (roomingHouse is null)
        {
            return null;
        }

        if (roomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Approved)
        {
            throw new ConflictException(
                ErrorCodes.HouseInvalidStatus,
                "Chỉ khu trọ đã được duyệt mới có thể bật/tắt hiển thị.",
                new { currentStatus = roomingHouse.ApprovalStatus.ToString() });
        }

        if (!Enum.TryParse<RoomingHouseVisibilityStatus>(request.VisibilityStatus, ignoreCase: true, out var visibilityStatus))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Trạng thái hiển thị khu trọ không hợp lệ.",
                new { field = nameof(request.VisibilityStatus) });
        }

        roomingHouse.VisibilityStatus = visibilityStatus;
        roomingHouse.UpdatedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return await queryService.GetByIdAsync(roomingHouseId, cancellationToken);
    }

    private async Task<string> BuildAddressDisplayAsync(
        string addressLine,
        string provinceCode,
        string wardCode,
        CancellationToken cancellationToken)
    {
        var province = provinceCode.Trim();
        var ward = wardCode.Trim();

        var provinceName = await context.AdministrativeProvinces
            .Where(x => x.Code == province && x.IsActive)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (provinceName is null)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Tỉnh/thành phố không hợp lệ.",
                new { field = nameof(provinceCode) });
        }

        var wardName = await context.AdministrativeWards
            .Where(x => x.Code == ward && x.ProvinceCode == province && x.IsActive)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (wardName is null)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Phường/xã không hợp lệ.",
                new { field = nameof(wardCode) });
        }

        return $"{addressLine.Trim()}, {wardName}, {provinceName}";
    }

    private static void ValidateRoomingHouseFields(RoomingHouseBasicInfoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Tên khu trọ là bắt buộc.", new { field = nameof(request.Name) });
        }

        if (string.IsNullOrWhiteSpace(request.AddressLine))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Địa chỉ là bắt buộc.", new { field = nameof(request.AddressLine) });
        }

        if (string.IsNullOrWhiteSpace(request.ProvinceCode))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Mã tỉnh/thành phố là bắt buộc.", new { field = nameof(request.ProvinceCode) });
        }

        if (string.IsNullOrWhiteSpace(request.WardCode))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Mã phường/xã là bắt buộc.", new { field = nameof(request.WardCode) });
        }

        if (request.Latitude is < -90 or > 90)
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Vĩ độ phải nằm trong khoảng từ -90 đến 90.", new { field = nameof(request.Latitude) });
        }

        if (request.Longitude is < -180 or > 180)
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Kinh độ phải nằm trong khoảng từ -180 đến 180.", new { field = nameof(request.Longitude) });
        }

        if (!string.IsNullOrWhiteSpace(request.GoogleMapUrl) &&
            !Uri.TryCreate(request.GoogleMapUrl.Trim(), UriKind.Absolute, out _))
        {
            throw new BadRequestException(ErrorCodes.ValidationError, "Link Google Map không hợp lệ.", new { field = nameof(request.GoogleMapUrl) });
        }
    }

    private static string? NormalizeOptionalUrl(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void EnsureAddressEditable(RoomingHouse roomingHouse, RoomingHouseBasicInfoRequest request)
    {
        if (roomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Approved)
        {
            return;
        }

        var addressChanged =
            !string.Equals(roomingHouse.AddressLine.Trim(), request.AddressLine.Trim(), StringComparison.Ordinal) ||
            !string.Equals(roomingHouse.ProvinceCode.Trim(), request.ProvinceCode.Trim(), StringComparison.Ordinal) ||
            !string.Equals(roomingHouse.WardCode.Trim(), request.WardCode.Trim(), StringComparison.Ordinal);

        if (!addressChanged)
        {
            return;
        }

        throw new ConflictException(
            ErrorCodes.HouseInvalidStatus,
            "Không thể chỉnh sửa địa chỉ hành chính của khu trọ đã được duyệt.",
            new
            {
                fields = new[] { nameof(request.AddressLine), nameof(request.ProvinceCode), nameof(request.WardCode) }
            });
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

    private static string BuildCreateDraftBlockedMessage(RoomingHouseApprovalStatus status)
    {
        return status switch
        {
            RoomingHouseApprovalStatus.Draft =>
                "Bạn đang có bản nháp khu trọ. Vui lòng hoàn thành bản nháp và gửi duyệt trước khi thêm khu trọ mới.",
            RoomingHouseApprovalStatus.Pending =>
                "Bạn đang có khu trọ chờ duyệt. Vui lòng chờ kết quả xét duyệt trước khi thêm khu trọ mới.",
            RoomingHouseApprovalStatus.Rejected =>
                "Bạn đang có khu trọ bị từ chối. Vui lòng chỉnh sửa và gửi duyệt lại trước khi thêm khu trọ mới.",
            _ =>
                "Bạn cần xử lý hồ sơ khu trọ hiện tại trước khi thêm khu trọ mới."
        };
    }
}
