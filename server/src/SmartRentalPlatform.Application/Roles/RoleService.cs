using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Users;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Roles;

public class RoleService : IRoleService
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public RoleService(
        IAppDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<UserRoleStatusResponse> GetUserRoleStatusAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Bạn cần đăng nhập để thực hiện thao tác này.");
        }

        var userId = _currentUserService.UserId.Value;

        var user = await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .Include(x => x.UserProfile)
            .Include(x => x.KycVerifications)
            .Include(x => x.RoomingHouses)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Token không còn hợp lệ.");
        }

        var isTenant = user.UserRoles.Any(ur => ur.Role.Name == RoleName.Tenant);
        var isLandlord = user.UserRoles.Any(ur => ur.Role.Name == RoleName.Landlord);
        var isAdmin = user.UserRoles.Any(ur => ur.Role.Name == RoleName.Admin);

        var latestKyc = user.KycVerifications
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        var latestRoomingHouse = user.RoomingHouses
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        return new UserRoleStatusResponse
        {
            UserId = user.Id,
            Roles = user.UserRoles
                .Select(x => x.Role.Name.ToString())
                .ToArray(),
            IsTenant = isTenant,
            IsLandlord = isLandlord,
            IsAdmin = isAdmin,
            OnboardingStatus = user.OnboardingStatus.ToString(),
            KycStatus = latestKyc?.Status.ToString() ?? "None",
            KycRejectReason = latestKyc?.RejectedReason,
            LandlordApplicationStatus = latestRoomingHouse?.ApprovalStatus.ToString() ?? "None",
            LandlordApplicationRejectReason = latestRoomingHouse?.RejectedReason
        };
    }

    public async Task AssignDefaultTenantRoleAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new BadRequestException(
                ErrorCodes.NotFound,
                $"Không tìm thấy người dùng với Id {userId}");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new ForbiddenException(
                ErrorCodes.UserBanned,
                "Tài khoản người dùng đang bị khóa hoặc bị xóa.");
        }

        if (!user.EmailConfirmed)
        {
            return;
        }

        var tenantRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == RoleName.Tenant, cancellationToken);

        if (tenantRole is null)
        {
            throw new BadRequestException(
                ErrorCodes.NotFound,
                "Không tìm thấy vai trò Tenant trong hệ thống.");
        }

        var hasTenantRole = user.UserRoles.Any(ur => ur.RoleId == tenantRole.Id);

        if (!hasTenantRole)
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = tenantRole.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task GrantLandlordRoleAfterRoomingHouseApprovedAsync(
        Guid roomingHouseId,
        CancellationToken cancellationToken = default)
    {
        var roomingHouse = await _dbContext.RoomingHouses
            .FirstOrDefaultAsync(rh => rh.Id == roomingHouseId, cancellationToken);

        if (roomingHouse is null)
        {
            throw new BadRequestException(
                ErrorCodes.HouseNotFound,
                $"Không tìm thấy khu trọ với Id {roomingHouseId}");
        }

        if (roomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Approved)
        {
            return;
        }

        var landlordUserId = roomingHouse.LandlordUserId;

        var latestKyc = await _dbContext.KycVerifications
            .Where(x => x.UserId == landlordUserId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestKyc is null || latestKyc.Status != KycVerificationStatus.Approved)
        {
            throw new BadRequestException(
                ErrorCodes.KycRequired,
                "Không thể cấp vai trò Chủ trọ vì người dùng chưa được duyệt KYC.");
        }

        var landlordRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == RoleName.Landlord, cancellationToken);

        if (landlordRole is null)
        {
            throw new BadRequestException(
                ErrorCodes.NotFound,
                "Không tìm thấy vai trò Landlord trong hệ thống.");
        }

        var hasLandlordRole = await _dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == landlordUserId && ur.RoleId == landlordRole.Id, cancellationToken);

        if (!hasLandlordRole)
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = landlordUserId,
                RoleId = landlordRole.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == landlordUserId, cancellationToken);

            if (user is not null && user.OnboardingStatus != OnboardingStatus.Completed)
            {
                user.OnboardingStatus = OnboardingStatus.Completed;
                user.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
