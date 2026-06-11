using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Auth;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Auth;

public class GoogleLoginService : IGoogleLoginService
{
    private readonly IAppDbContext dbContext;
    private readonly ITokenService tokenService;
    private readonly IGoogleAuthService googleAuthService;
    private readonly IHttpContextAccessor httpContextAccessor;

    public GoogleLoginService(
        IAppDbContext dbContext,
        ITokenService tokenService,
        IGoogleAuthService googleAuthService,
        IHttpContextAccessor httpContextAccessor)
    {
        this.dbContext = dbContext;
        this.tokenService = tokenService;
        this.googleAuthService = googleAuthService;
        this.httpContextAccessor = httpContextAccessor;
    }

    public async Task<GoogleLoginResponse> GoogleLoginAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var googleUser = await googleAuthService.VerifyIdTokenAsync(request.IdToken.Trim(), cancellationToken);
        var normalizedEmail = googleUser.Email.Trim().ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;

        var externalLogin = await dbContext.ExternalLogins
            .Include(x => x.User)
                .ThenInclude(x => x.UserRoles)
                    .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Provider == LoginProvider.Google &&
                     x.ProviderUserId == googleUser.ProviderUserId,
                cancellationToken);

        User user;

        if (externalLogin is not null)
        {
            user = externalLogin.User;
            externalLogin.LastLoginAt = now;
            SyncGoogleAvatar(user, externalLogin, googleUser.AvatarUrl, now);
        }
        else
        {
            user = await dbContext.Users
                .Include(x => x.UserRoles)
                    .ThenInclude(x => x.Role)
                .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken)
                ?? await CreateGoogleUserAsync(googleUser, now, cancellationToken);

            if (string.IsNullOrEmpty(user.AvatarUrl))
            {
                user.AvatarUrl = googleUser.AvatarUrl;
                user.UpdatedAt = now;
            }

            dbContext.ExternalLogins.Add(new ExternalLogin
            {
                UserId = user.Id,
                Provider = LoginProvider.Google,
                ProviderUserId = googleUser.ProviderUserId,
                ProviderEmail = googleUser.Email.Trim(),
                ProviderDisplayName = googleUser.DisplayName,
                ProviderAvatarUrl = googleUser.AvatarUrl,
                CreatedAt = now,
                LastLoginAt = now
            });
        }

        EnsureUserCanLogin(user, now);

        var roles = user.UserRoles.Select(x => x.Role.Name.ToString()).ToArray();

        if (!user.EmailConfirmed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return BuildGoogleLoginResponse(user, roles, true, null, null);
        }

        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var refreshToken = tokenService.GenerateRefreshToken();

        user.LastLoginAt = now;

        dbContext.LoginLogs.Add(new LoginLog
        {
            UserId = user.Id,
            EmailAttempted = googleUser.Email.Trim(),
            LoginProvider = LoginProvider.Google,
            IsSuccess = true
        });

        dbContext.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            TokenType = TokenType.Refresh,
            TokenHash = tokenService.HashToken(refreshToken),
            TokenFamilyId = Guid.NewGuid(),
            ExpiresAt = now.AddDays(30),
            CreatedAt = now,
            CreatedByIp = GetCurrentIp(),
            UserAgent = GetCurrentUserAgent()
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return BuildGoogleLoginResponse(user, roles, false, accessToken, refreshToken);
    }

    private async Task<User> CreateGoogleUserAsync(
        GoogleUserInfo googleUser,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Email = googleUser.Email.Trim(),
            NormalizedEmail = googleUser.Email.Trim().ToUpperInvariant(),
            DisplayName = string.IsNullOrWhiteSpace(googleUser.DisplayName)
                ? googleUser.Email.Trim()
                : googleUser.DisplayName.Trim(),
            AvatarUrl = googleUser.AvatarUrl,
            PasswordHash = null,
            Status = UserStatus.Active,
            OnboardingStatus = OnboardingStatus.NeedProfileUpdate,
            EmailConfirmed = true,
            PhoneConfirmed = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var tenantRole = await dbContext.Roles.FirstAsync(x => x.Name == RoleName.Tenant, cancellationToken);

        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = tenantRole.Id,
            Role = tenantRole
        });

        dbContext.Users.Add(user);
        return user;
    }

    private static void SyncGoogleAvatar(
        User user,
        ExternalLogin externalLogin,
        string? newAvatarUrl,
        DateTimeOffset now)
    {
        if (externalLogin.ProviderAvatarUrl != newAvatarUrl)
        {
            if (user.AvatarUrl == externalLogin.ProviderAvatarUrl)
            {
                user.AvatarUrl = newAvatarUrl;
                user.UpdatedAt = now;
            }

            externalLogin.ProviderAvatarUrl = newAvatarUrl;
        }
        else if (string.IsNullOrEmpty(user.AvatarUrl))
        {
            user.AvatarUrl = newAvatarUrl;
            user.UpdatedAt = now;
        }
    }

    private static void EnsureUserCanLogin(User user, DateTimeOffset now)
    {
        if (user.Status == UserStatus.Banned)
        {
            throw new ForbiddenException(ErrorCodes.UserBanned, "Tài khoản đã bị khóa bởi hệ thống.");
        }

        if (user.Status == UserStatus.Deleted)
        {
            throw new ForbiddenException(ErrorCodes.UserDeleted, "Tài khoản không còn tồn tại.");
        }

        if (user.LockoutEndAt is not null && user.LockoutEndAt > now)
        {
            throw new ForbiddenException(ErrorCodes.UserLocked, "Tài khoản đang bị khóa tạm thời.");
        }
    }

    private static GoogleLoginResponse BuildGoogleLoginResponse(
        User user,
        IReadOnlyCollection<string> roles,
        bool requiresEmailVerification,
        string? accessToken,
        string? refreshToken)
    {
        return new GoogleLoginResponse
        {
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            IsGoogleUser = true,
            EmailConfirmed = user.EmailConfirmed,
            RequiresEmailVerification = requiresEmailVerification,
            Status = user.Status.ToString(),
            OnboardingStatus = user.OnboardingStatus.ToString(),
            Roles = roles,
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
    }

    private string? GetCurrentIp()
    {
        var ip = httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        return ip == "::1" ? "127.0.0.1" : ip;
    }

    private string? GetCurrentUserAgent()
    {
        return httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString();
    }
}
