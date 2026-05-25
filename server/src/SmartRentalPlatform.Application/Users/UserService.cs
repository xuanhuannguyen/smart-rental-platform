using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Users;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Users;

public class UserService : IUserService
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UserService(
        IAppDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(
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
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Token không còn hợp lệ.");
        }

        return new CurrentUserResponse
        {
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            EmailConfirmed = user.EmailConfirmed,
            Status = user.Status.ToString(),
            OnboardingStatus = user.OnboardingStatus.ToString(),
            Roles = user.UserRoles
                .Select(x => x.Role.Name.ToString())
                .ToArray()
        };
    }

    public async Task<UserProfileResponse> GetUserProfileAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Bạn cần đăng nhập để thực hiện thao tác này.");
        }

        var userId = _currentUserService.UserId.Value;

        var profile = await _dbContext.UserProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (profile is null)
        {
            return new UserProfileResponse
            {
                UserId = userId,
                ProfileCompleted = false
            };
        }

        var isCompleted = !string.IsNullOrWhiteSpace(profile.FullName) &&
            profile.DateOfBirth != default &&
            !string.IsNullOrWhiteSpace(profile.AddressLine);

        return new UserProfileResponse
        {
            UserId = profile.UserId,
            FullName = profile.FullName,
            DateOfBirth = profile.DateOfBirth,
            Gender = profile.Gender,
            AddressLine = profile.AddressLine,
            EmergencyContactName = profile.EmergencyContactName,
            EmergencyContactPhone = profile.EmergencyContactPhone,
            VerifiedCitizenIdMasked = profile.VerifiedCitizenIdMasked,
            ProfileCompleted = isCompleted
        };
    }

    public async Task<UserProfileResponse> UpdateUserProfileAsync(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Bạn cần đăng nhập để thực hiện thao tác này.");
        }

        var userId = _currentUserService.UserId.Value;

        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.Today))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Ngày sinh không được là ngày ở tương lai.");
        }

        var user = await _dbContext.Users
            .Include(x => x.UserProfile)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Token không còn hợp lệ.");
        }

        var now = DateTimeOffset.UtcNow;
        var profile = user.UserProfile;

        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                CreatedAt = now
            };
            user.UserProfile = profile;
            _dbContext.UserProfiles.Add(profile);
        }

        profile.FullName = request.FullName.Trim();
        profile.DateOfBirth = request.DateOfBirth;
        profile.Gender = request.Gender?.Trim();
        profile.AddressLine = request.AddressLine.Trim();
        profile.EmergencyContactName = request.EmergencyContactName?.Trim();
        profile.EmergencyContactPhone = request.EmergencyContactPhone?.Trim();
        profile.UpdatedAt = now;

        if (user.OnboardingStatus == OnboardingStatus.NeedProfileUpdate)
        {
            user.OnboardingStatus = OnboardingStatus.NeedKyc;
            user.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UserProfileResponse
        {
            UserId = userId,
            FullName = profile.FullName,
            DateOfBirth = profile.DateOfBirth,
            Gender = profile.Gender,
            AddressLine = profile.AddressLine,
            EmergencyContactName = profile.EmergencyContactName,
            EmergencyContactPhone = profile.EmergencyContactPhone,
            VerifiedCitizenIdMasked = profile.VerifiedCitizenIdMasked,
            ProfileCompleted = true
        };
    }

    public async Task<LandlordEligibilityResponse> GetLandlordEligibilityAsync(
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

        // 1. Check Email Confirmed
        if (!user.EmailConfirmed)
        {
            return new LandlordEligibilityResponse
            {
                CanContinue = false,
                NextStep = "VerifyEmailPage",
                Reason = "EMAIL_NOT_CONFIRMED"
            };
        }

        // 2. Check Profile Completed
        var isProfileCompleted = user.UserProfile is not null &&
            !string.IsNullOrWhiteSpace(user.UserProfile.FullName) &&
            user.UserProfile.DateOfBirth != default &&
            !string.IsNullOrWhiteSpace(user.UserProfile.AddressLine);

        if (!isProfileCompleted)
        {
            return new LandlordEligibilityResponse
            {
                CanContinue = false,
                NextStep = "CompleteProfilePage",
                Reason = "PROFILE_INCOMPLETE"
            };
        }

        // 3. Check KYC Approved
        var latestKyc = user.KycVerifications
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (latestKyc is null || latestKyc.Status != KycVerificationStatus.Approved)
        {
            var nextStep = (latestKyc is not null && latestKyc.Status == KycVerificationStatus.Pending)
                ? "KycStatusPage"
                : "KycSubmitPage";

            return new LandlordEligibilityResponse
            {
                CanContinue = false,
                NextStep = nextStep,
                Reason = "KYC_NOT_APPROVED"
            };
        }

        // 4. Check if already Landlord
        var hasLandlordRole = user.UserRoles
            .Any(ur => ur.Role.Name == RoleName.Landlord);

        if (hasLandlordRole)
        {
            return new LandlordEligibilityResponse
            {
                CanContinue = false,
                NextStep = "LandlordDashboard",
                Reason = "ALREADY_LANDLORD"
            };
        }

        // 5. Check if has a pending RoomingHouse registration
        var hasPendingRoomingHouse = user.RoomingHouses
            .Any(rh => rh.ApprovalStatus == RoomingHouseApprovalStatus.Pending);

        if (hasPendingRoomingHouse)
        {
            return new LandlordEligibilityResponse
            {
                CanContinue = false,
                NextStep = "LandlordApplicationStatusPage",
                Reason = "HAS_PENDING_ROOMING_HOUSE"
            };
        }

        // 6. Otherwise: Can Register
        return new LandlordEligibilityResponse
        {
            CanContinue = true,
            NextStep = "LandlordApplicationPage",
            Reason = null
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
