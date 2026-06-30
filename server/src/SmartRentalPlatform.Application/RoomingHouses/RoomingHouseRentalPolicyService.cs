using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalPolicies.Requests;
using SmartRentalPlatform.Contracts.RentalPolicies.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.RoomingHouses;

public class RoomingHouseRentalPolicyService : IRoomingHouseRentalPolicyService
{
    private readonly IAppDbContext context;

    public RoomingHouseRentalPolicyService(IAppDbContext context)
    {
        this.context = context;
    }

    public async Task<RentalPolicyResponse?> GetRentalPolicyAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        var rentalPolicy = await context.RentalPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoomingHouseId == roomingHouseId, cancellationToken);

        return rentalPolicy is null ? null : ToRentalPolicyResponse(rentalPolicy);
    }

    public async Task<RentalPolicyResponse> UpdateRentalPolicyAsync(
        Guid roomingHouseId,
        Guid landlordUserId,
        UpdateRentalPolicyRequest request,
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

        ValidateRentalPolicyFields(request);

        var now = DateTimeOffset.UtcNow;
        var rentalPolicy = await context.RentalPolicies
            .FirstOrDefaultAsync(x => x.RoomingHouseId == roomingHouseId, cancellationToken);

        if (rentalPolicy is null)
        {
            rentalPolicy = new RentalPolicy
            {
                Id = Guid.NewGuid(),
                RoomingHouseId = roomingHouseId,
                CreatedAt = now
            };
            context.RentalPolicies.Add(rentalPolicy);
        }

        rentalPolicy.MinRentalMonths = request.MinRentalMonths;
        rentalPolicy.MaxRentalMonths = request.MaxRentalMonths;
        rentalPolicy.AllowShortTermRenewal = request.AllowShortTermRenewal;
        rentalPolicy.RenewalNoticeDays = request.RenewalNoticeDays;
        rentalPolicy.DepositMonths = request.DepositMonths;
        rentalPolicy.DefaultPaymentDay = request.DefaultPaymentDay;
        rentalPolicy.IsActive = true;
        rentalPolicy.UpdatedAt = now;

        roomingHouse.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        return ToRentalPolicyResponse(rentalPolicy);
    }

    private static void ValidateRentalPolicyFields(UpdateRentalPolicyRequest request)
    {
        if (request.MinRentalMonths <= 0)
        {
            throw new BadRequestException(
                ErrorCodes.RentalPolicyInvalid,
                "Số tháng thuê tối thiểu phải lớn hơn 0.",
                new { field = nameof(request.MinRentalMonths) });
        }

        if (request.MaxRentalMonths < request.MinRentalMonths)
        {
            throw new BadRequestException(
                ErrorCodes.RentalPolicyInvalid,
                "Số tháng thuê tối đa phải lớn hơn hoặc bằng số tháng thuê tối thiểu.",
                new { field = nameof(request.MaxRentalMonths) });
        }

        if (request.RenewalNoticeDays < 0)
        {
            throw new BadRequestException(
                ErrorCodes.RentalPolicyInvalid,
                "Số ngày báo trước khi gia hạn phải lớn hơn hoặc bằng 0.",
                new { field = nameof(request.RenewalNoticeDays) });
        }

        if (request.DepositMonths < 0)
        {
            throw new BadRequestException(
                ErrorCodes.RentalPolicyInvalid,
                "Số tháng tiền thuê dùng để tính tiền cọc phải lớn hơn hoặc bằng 0.",
                new { field = nameof(request.DepositMonths) });
        }

        if (request.DefaultPaymentDay is < 1 or > 28)
        {
            throw new BadRequestException(
                ErrorCodes.RentalPolicyInvalid,
                "Ngày thanh toán mặc định phải nằm trong khoảng từ 1 đến 28.",
                new { field = nameof(request.DefaultPaymentDay) });
        }
    }

    private static RentalPolicyResponse ToRentalPolicyResponse(RentalPolicy rentalPolicy)
    {
        return new RentalPolicyResponse
        {
            Id = rentalPolicy.Id,
            RoomingHouseId = rentalPolicy.RoomingHouseId,
            MinRentalMonths = rentalPolicy.MinRentalMonths,
            MaxRentalMonths = rentalPolicy.MaxRentalMonths,
            AllowShortTermRenewal = rentalPolicy.AllowShortTermRenewal,
            RenewalNoticeDays = rentalPolicy.RenewalNoticeDays,
            DepositMonths = rentalPolicy.DepositMonths,
            DefaultPaymentDay = rentalPolicy.DefaultPaymentDay,
            IsActive = rentalPolicy.IsActive,
            CreatedAt = rentalPolicy.CreatedAt,
            UpdatedAt = rentalPolicy.UpdatedAt
        };
    }
}
