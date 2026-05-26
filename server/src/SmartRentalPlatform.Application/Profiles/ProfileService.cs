using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Profiles;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Profiles;

public class ProfileService : IProfileService
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ProfileService(
        IAppDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
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
}
