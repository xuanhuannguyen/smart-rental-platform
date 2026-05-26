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
}
