using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.Users;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Application.Users;

public class UserService : IUserService
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserService(
        IAppDbContext dbContext,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
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

        var avatarUrl = await AvatarMediaUrlResolver.ResolveAsync(
            _dbContext,
            user.AvatarUrl,
            user.AvatarMediaAssetId,
            cancellationToken);

        return new CurrentUserResponse
        {
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = avatarUrl,
            AvatarMediaAssetId = user.AvatarMediaAssetId,
            IsGoogleUser = string.IsNullOrEmpty(user.PasswordHash),
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

        var user = await _dbContext.Users
            .Include(x => x.UserProfile)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Token không còn hợp lệ.");
        }

        var latestKyc = await _dbContext.KycVerifications
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var approvedKyc = await _dbContext.KycVerifications
            .Where(x => x.UserId == userId && x.Status == KycVerificationStatus.Approved)
            .OrderByDescending(x => x.ReviewedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);



        var profile = user.UserProfile;
        var identityVerified = approvedKyc is not null;
        var profileCompleted = identityVerified &&
            !string.IsNullOrWhiteSpace(profile?.FullName) &&
            profile?.DateOfBirth != null &&
            !string.IsNullOrWhiteSpace(profile?.AddressLine);
        var avatarUrl = await AvatarMediaUrlResolver.ResolveAsync(
            _dbContext,
            user.AvatarUrl,
            user.AvatarMediaAssetId,
            cancellationToken);

        return new UserProfileResponse
        {
            UserId = userId,
            DisplayName = user.DisplayName,
            PhoneNumber = user.PhoneNumber,
            FullName = profile?.FullName,
            DateOfBirth = profile?.DateOfBirth,
            Gender = profile?.Gender,
            AddressLine = profile?.AddressLine,
            EmergencyContactName = profile?.EmergencyContactName,
            EmergencyContactPhone = profile?.EmergencyContactPhone,
            VerifiedCitizenIdMasked = profile?.VerifiedCitizenIdMasked,
            KycStatus = latestKyc?.Status.ToString(),
            KycReviewedAt = approvedKyc?.ReviewedAt,
            IdentityVerified = approvedKyc is not null,
            ProfileCompleted = profileCompleted,
            AvatarUrl = avatarUrl,
            AvatarMediaAssetId = user.AvatarMediaAssetId,
            IsGoogleUser = string.IsNullOrEmpty(user.PasswordHash)
        };
    }

    public async Task<OccupantAccountLookupResponse> LookupOccupantAccountAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Email người ở không được để trống.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Email.ToLower() == normalizedEmail && x.DeletedAt == null,
                cancellationToken);

        if (user is null)
        {
            return new OccupantAccountLookupResponse
            {
                Email = normalizedEmail,
                Exists = false,
                IsKycApproved = false
            };
        }

        var isKycApproved = await _dbContext.KycVerifications
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == user.Id && x.Status == KycVerificationStatus.Approved,
                cancellationToken);

        return new OccupantAccountLookupResponse
        {
            Email = user.Email,
            Exists = true,
            IsKycApproved = isKycApproved,
            DisplayName = user.DisplayName
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

        var user = await _dbContext.Users
            .Include(x => x.UserProfile)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Token không còn hợp lệ.");
        }

        // Cập nhật thông tin đơn giản
        user.DisplayName = request.DisplayName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();

        var now = DateTimeOffset.UtcNow;
        var previousAvatarMediaAssetId = user.AvatarMediaAssetId;
        user.AvatarMediaAssetId = await ResolveAvatarMediaAssetAsync(
            userId,
            request.AvatarMediaAssetId,
            now,
            cancellationToken);
        if (previousAvatarMediaAssetId.HasValue &&
            previousAvatarMediaAssetId.Value != user.AvatarMediaAssetId)
        {
            await RetireAvatarMediaAssetAsync(previousAvatarMediaAssetId.Value, userId, now, cancellationToken);
        }

        if (user.AvatarMediaAssetId.HasValue)
        {
            user.AvatarUrl = null;
        }

        var profile = await _dbContext.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

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

        profile.EmergencyContactName = request.EmergencyContactName?.Trim();
        profile.EmergencyContactPhone = request.EmergencyContactPhone?.Trim();
        profile.UpdatedAt = now;
        user.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Lấy thông tin KYC để xác định trạng thái
        var approvedKyc = await _dbContext.KycVerifications
            .Where(x => x.UserId == userId && x.Status == KycVerificationStatus.Approved)
            .OrderByDescending(x => x.ReviewedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var latestKyc = await _dbContext.KycVerifications
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var identityVerified = approvedKyc is not null;
        var profileCompleted = identityVerified &&
            !string.IsNullOrWhiteSpace(profile.FullName) &&
            profile.DateOfBirth != null &&
            !string.IsNullOrWhiteSpace(profile.AddressLine);
        var avatarUrl = await AvatarMediaUrlResolver.ResolveAsync(
            _dbContext,
            user.AvatarUrl,
            user.AvatarMediaAssetId,
            cancellationToken);

        return new UserProfileResponse
        {
            UserId = userId,
            DisplayName = user.DisplayName,
            PhoneNumber = user.PhoneNumber,
            FullName = profile.FullName,
            DateOfBirth = profile.DateOfBirth,
            Gender = profile.Gender,
            AddressLine = profile.AddressLine,
            EmergencyContactName = profile.EmergencyContactName,
            EmergencyContactPhone = profile.EmergencyContactPhone,
            VerifiedCitizenIdMasked = profile.VerifiedCitizenIdMasked,
            KycStatus = latestKyc?.Status.ToString(),
            KycReviewedAt = approvedKyc?.ReviewedAt,
            IdentityVerified = identityVerified,
            ProfileCompleted = profileCompleted,
            AvatarUrl = avatarUrl,
            AvatarMediaAssetId = user.AvatarMediaAssetId,
            IsGoogleUser = string.IsNullOrEmpty(user.PasswordHash)
        };
    }

    private async Task<Guid?> ResolveAvatarMediaAssetAsync(
        Guid userId,
        Guid? avatarMediaAssetId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!avatarMediaAssetId.HasValue)
        {
            return null;
        }

        MediaAsset? mediaAsset = await _dbContext.MediaAssets
            .FirstOrDefaultAsync(x => x.Id == avatarMediaAssetId.Value, cancellationToken);

        if (mediaAsset is null)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Avatar media asset không tồn tại.");
        }

        if (mediaAsset.Scope != MediaScope.Avatar)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset được chọn không phải avatar hợp lệ.");
        }

        if (mediaAsset.Visibility != MediaVisibility.Public ||
            mediaAsset.Status is MediaStatus.PendingUpload or MediaStatus.Deleted)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Avatar media asset chưa sẵn sàng để sử dụng.");
        }

        if (mediaAsset.OwnerUserId.HasValue && mediaAsset.OwnerUserId.Value != userId)
        {
            throw new ForbiddenException(
                ErrorCodes.Forbidden,
                "Bạn không có quyền sử dụng avatar media asset này.");
        }

        mediaAsset.OwnerUserId = userId;
        mediaAsset.Scope = MediaScope.Avatar;
        mediaAsset.Visibility = MediaVisibility.Public;
        mediaAsset.Status = MediaStatus.Linked;
        mediaAsset.LinkedEntityType = nameof(User);
        mediaAsset.LinkedEntityId = userId;
        mediaAsset.DeletedAt = null;
        mediaAsset.UpdatedAt = now;

        return mediaAsset.Id;
    }

    private async Task RetireAvatarMediaAssetAsync(
        Guid avatarMediaAssetId,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mediaAsset = await _dbContext.MediaAssets
            .FirstOrDefaultAsync(x => x.Id == avatarMediaAssetId, cancellationToken);

        if (mediaAsset is null || mediaAsset.Scope != MediaScope.Avatar)
        {
            return;
        }

        var belongsToUser =
            mediaAsset.OwnerUserId == userId ||
            (mediaAsset.LinkedEntityType == nameof(User) && mediaAsset.LinkedEntityId == userId);

        if (!belongsToUser)
        {
            return;
        }

        mediaAsset.LinkedEntityType = null;
        mediaAsset.LinkedEntityId = null;
        mediaAsset.Status = MediaStatus.Deleted;
        mediaAsset.DeletedAt = now;
        mediaAsset.UpdatedAt = now;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? null : trimmed;
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
        var latestApprovedKyc = user.KycVerifications
            .Where(x => x.Status == KycVerificationStatus.Approved)
            .OrderByDescending(x => x.ReviewedAt ?? x.CreatedAt)
            .FirstOrDefault();

        var isProfileCompleted = latestApprovedKyc is not null &&
            !string.IsNullOrWhiteSpace(latestApprovedKyc.OcrFullName) &&
            latestApprovedKyc.OcrDateOfBirth != null &&
            !string.IsNullOrWhiteSpace(latestApprovedKyc.OcrAddress);

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
            var isWaitingReview = latestKyc is not null &&
                latestKyc.Status is KycVerificationStatus.Pending or
                    KycVerificationStatus.PendingEkyc or
                    KycVerificationStatus.PendingAdminReview;

            var nextStep = isWaitingReview
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



    private static bool SetIfDifferent<T>(T current, T next, Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, next))
        {
            return false;
        }

        assign(next);
        return true;
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

    public async Task<IReadOnlyCollection<UserSessionResponse>> GetActiveSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Bạn cần đăng nhập để thực hiện thao tác này.");
        }

        var userId = _currentUserService.UserId.Value;

        var activeTokens = await _dbContext.UserTokens
            .Where(x => x.UserId == userId &&
                        x.TokenType == TokenType.Refresh &&
                        x.RevokedAt == null &&
                        x.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var currentIp = GetCurrentIp();
        var currentUserAgent = GetCurrentUserAgent();

        return activeTokens.Select(token => new UserSessionResponse
        {
            Id = token.Id,
            IpAddress = token.CreatedByIp,
            UserAgent = token.UserAgent,
            CreatedAt = token.CreatedAt,
            ExpiresAt = token.ExpiresAt,
            IsCurrentSession = IsCurrentSession(token.CreatedByIp, token.UserAgent, currentIp, currentUserAgent)
        }).ToList();
    }

    public async Task RevokeSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            throw new UnauthorizedException(
                ErrorCodes.Unauthorized,
                "Bạn cần đăng nhập để thực hiện thao tác này.");
        }

        var userId = _currentUserService.UserId.Value;

        var token = await _dbContext.UserTokens
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, cancellationToken);

        if (token is null)
        {
            throw new NotFoundException(
                ErrorCodes.NotFound,
                "Không tìm thấy phiên đăng nhập.");
        }

        if (token.RevokedAt is null)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            token.RevokedReason = TokenRevokedReason.Logout;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private string? GetCurrentIp()
    {
        var context = _httpContextAccessor?.HttpContext;
        var ip = context?.Connection?.RemoteIpAddress?.ToString();
        if (ip == "::1") return "127.0.0.1";
        return ip;
    }

    private string? GetCurrentUserAgent()
    {
        var context = _httpContextAccessor?.HttpContext;
        return context?.Request?.Headers["User-Agent"].ToString();
    }

    private static bool IsCurrentSession(string? tokenIp, string? tokenUa, string? currentIp, string? currentUa)
    {
        if (string.IsNullOrEmpty(tokenUa) || string.IsNullOrEmpty(currentUa))
            return false;

        if (!string.Equals(tokenUa, currentUa, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(tokenIp, currentIp, StringComparison.OrdinalIgnoreCase))
            return true;

        if (IsLocalhost(tokenIp) && IsLocalhost(currentIp))
            return true;

        return false;
    }

    private static bool IsLocalhost(string? ip)
    {
        if (string.IsNullOrEmpty(ip)) return false;
        return ip == "::1" || ip == "127.0.0.1" || ip.Equals("localhost", StringComparison.OrdinalIgnoreCase);
    }
}
