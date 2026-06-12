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
                "Khong tim thay khu tro.",
                new { roomingHouseId });
        }

        if (roomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Approved)
        {
            throw new ConflictException(
                ErrorCodes.HouseNotApproved,
                "Chi khu tro da duoc duyet moi co the cau hinh chinh sach thue.",
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
                "So thang thue toi thieu phai lon hon 0.",
                new { field = nameof(request.MinRentalMonths) });
        }

        if (request.MaxRentalMonths < request.MinRentalMonths)
        {
            throw new BadRequestException(
                ErrorCodes.RentalPolicyInvalid,
                "So thang thue toi da phai lon hon hoac bang so thang thue toi thieu.",
                new { field = nameof(request.MaxRentalMonths) });
        }

        if (request.RenewalNoticeDays < 0)
        {
            throw new BadRequestException(
                ErrorCodes.RentalPolicyInvalid,
                "So ngay bao truoc khi gia han phai lon hon hoac bang 0.",
                new { field = nameof(request.RenewalNoticeDays) });
        }

        if (request.DepositMonths < 0)
        {
            throw new BadRequestException(
                ErrorCodes.RentalPolicyInvalid,
                "So thang tien thue dung de tinh tien coc phai lon hon hoac bang 0.",
                new { field = nameof(request.DepositMonths) });
        }

        if (request.DefaultPaymentDay is < 1 or > 28)
        {
            throw new BadRequestException(
                ErrorCodes.RentalPolicyInvalid,
                "Ngay thanh toan mac dinh phai nam trong khoang tu 1 den 28.",
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
