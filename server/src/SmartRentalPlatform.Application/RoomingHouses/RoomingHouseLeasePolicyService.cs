using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.LeasePolicies;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.RoomingHouses;

public class RoomingHouseLeasePolicyService : IRoomingHouseLeasePolicyService
{
    private readonly IAppDbContext context;

    public RoomingHouseLeasePolicyService(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task<LeasePolicyResponse?> GetLeasePolicyAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        var leasePolicy = await context.LeasePolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoomingHouseId == roomingHouseId, cancellationToken);

        return leasePolicy is null ? null : ToLeasePolicyResponse(leasePolicy);
    }

    public async Task<LeasePolicyResponse> UpdateLeasePolicyAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        UpdateLeasePolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        var roomingHouse = await context.RoomingHouses
            .FirstOrDefaultAsync(
                x => x.Id == roomingHouseId &&
                     x.LandlordUserId == landlordUserId &&
                     x.DeletedAt == null,
                cancellationToken);

        if (roomingHouse is null)
        {
            throw new NotFoundException(
                ErrorCodes.HouseNotFound,
                "Không tìm thấy khu trọ.",
                new { roomingHouseId });
        }

        if (roomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Approved)
        {
            throw new ConflictException(
                ErrorCodes.HouseNotApproved,
                "Chỉ khu trọ đã được duyệt mới có thể cấu hình chính sách thuê.",
                new { currentStatus = roomingHouse.ApprovalStatus.ToString() });
        }

        ValidateLeasePolicyFields(request);

        var now = DateTimeOffset.UtcNow;
        var leasePolicy = await context.LeasePolicies
            .FirstOrDefaultAsync(x => x.RoomingHouseId == roomingHouseId, cancellationToken);

        if (leasePolicy is null)
        {
            leasePolicy = new LeasePolicy
            {
                Id = Guid.NewGuid(),
                RoomingHouseId = roomingHouseId,
                CreatedAt = now
            };
            context.LeasePolicies.Add(leasePolicy);
        }

        leasePolicy.AllowShortTermRenewal = request.AllowShortTermRenewal;
        leasePolicy.RenewalNoticeDays = request.RenewalNoticeDays;
        leasePolicy.DepositMonths = request.DepositMonths;
        leasePolicy.Discount6MonthsPercent = request.Discount6MonthsPercent;
        leasePolicy.Discount9MonthsPercent = request.Discount9MonthsPercent;
        leasePolicy.Discount12MonthsPercent = request.Discount12MonthsPercent;
        leasePolicy.Discount24MonthsPercent = request.Discount24MonthsPercent;
        leasePolicy.IsActive = true;
        leasePolicy.UpdatedAt = now;

        roomingHouse.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        return ToLeasePolicyResponse(leasePolicy);
    }

    private static void ValidateLeasePolicyFields(UpdateLeasePolicyRequest request)
    {
        if (request.DepositMonths < 0)
        {
            throw new BadRequestException(
                ErrorCodes.LeasePolicyInvalid,
                "Số tháng tiền cọc phải lớn hơn hoặc bằng 0.",
                new { field = nameof(request.DepositMonths) });
        }

        if (request.RenewalNoticeDays < 0)
        {
            throw new BadRequestException(
                ErrorCodes.LeasePolicyInvalid,
                "Số ngày báo trước phải lớn hơn hoặc bằng 0.",
                new { field = nameof(request.RenewalNoticeDays) });
        }

        ValidateDiscountPercent(request.Discount6MonthsPercent, nameof(request.Discount6MonthsPercent), "6 tháng");
        ValidateDiscountPercent(request.Discount9MonthsPercent, nameof(request.Discount9MonthsPercent), "9 tháng");
        ValidateDiscountPercent(request.Discount12MonthsPercent, nameof(request.Discount12MonthsPercent), "12 tháng");
        ValidateDiscountPercent(request.Discount24MonthsPercent, nameof(request.Discount24MonthsPercent), "24 tháng");
    }

    private static void ValidateDiscountPercent(decimal value, string fieldName, string label)
    {
        if (value < 0 || value > 100)
        {
            throw new BadRequestException(
                ErrorCodes.LeasePolicyInvalid,
                $"Phần trăm giảm giá {label} phải từ 0 đến 100.",
                new { field = fieldName });
        }
    }

    private static LeasePolicyResponse ToLeasePolicyResponse(LeasePolicy leasePolicy)
    {
        return new LeasePolicyResponse
        {
            Id = leasePolicy.Id,
            RoomingHouseId = leasePolicy.RoomingHouseId,
            AllowShortTermRenewal = leasePolicy.AllowShortTermRenewal,
            RenewalNoticeDays = leasePolicy.RenewalNoticeDays,
            DepositMonths = leasePolicy.DepositMonths,
            Discount6MonthsPercent = leasePolicy.Discount6MonthsPercent,
            Discount9MonthsPercent = leasePolicy.Discount9MonthsPercent,
            Discount12MonthsPercent = leasePolicy.Discount12MonthsPercent,
            Discount24MonthsPercent = leasePolicy.Discount24MonthsPercent,
            IsActive = leasePolicy.IsActive,
            CreatedAt = leasePolicy.CreatedAt,
            UpdatedAt = leasePolicy.UpdatedAt
        };
    }
}
