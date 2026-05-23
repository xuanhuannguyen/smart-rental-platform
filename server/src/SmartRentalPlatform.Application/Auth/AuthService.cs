using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Auth;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IAppDbContext _dbContext;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGoogleAuthService _googleAuthService;

    public AuthService(
        IAppDbContext dbContext,
        IPasswordService passwordService,
        ITokenService tokenService,
        IEmailSender emailSender,
        ICurrentUserService currentUserService,
        IGoogleAuthService googleAuthService
    )
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _currentUserService = currentUserService;
        _googleAuthService = googleAuthService;
    }

    public async Task<RegisterResponse> RegisterAsync (
        RegisterRequest request,
        CancellationToken cancellationToken = default )
        {
             var normalizeEmail = request.Email.Trim().ToUpperInvariant();
             var existingUser = await _dbContext.Users.FirstOrDefaultAsync(
                x => x.NormalizedEmail == normalizeEmail , cancellationToken
             );
             if (existingUser is not null) {
                if (string.IsNullOrWhiteSpace(existingUser.PasswordHash))
                {
                    throw new ConflictException(
                        ErrorCodes.GoogleAccountExists,
                        "Email đã tồn tại bằng tài khoản Google. Vui lòng đăng nhập Google hoặc đặt mật khẩu bằng quên mật khẩu.");
                }

                throw new ConflictException(
                    ErrorCodes.EmailAlreadyExists,
                    "Email đã tồn tại.");
             }
             var user = new User
             {
                Email = request.Email.Trim(),
                NormalizedEmail = normalizeEmail,
                PhoneNumber = request.PhoneNumber?.Trim(),
                DisplayName = request.DisplayName.Trim(),
                PasswordHash = _passwordService.HashPassword(request.Password),
                Status = UserStatus.Active,
                OnboardingStatus = OnboardingStatus.NeedProfileUpdate,
                EmailConfirmed = false,
                PhoneConfirmed = false
             };
             var tenantRole = await _dbContext.Roles.FirstAsync( x => x.Name == RoleName.Tenant, cancellationToken);
             _dbContext.UserRoles.Add( new UserRole
             {
                UserId = user.Id,
                RoleId = tenantRole.Id
             });
             var otp = _tokenService.GenerateOtp();
             var otpHash = _tokenService.HashToken(otp);

             user.UserTokens.Add( new UserToken
             {
                UserId = user.Id,
                TokenType = TokenType.VerifyEmail,
                TokenHash = otpHash,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedAt = DateTime.UtcNow
             });
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _emailSender.SendEmailVerificationOtpAsync (
                user.Email,
                user.DisplayName,
                otp,
                cancellationToken
            );

            return new RegisterResponse
            {
                UserId = user.Id,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                Status = user.Status.ToString(),
                OnboardingStatus = user.OnboardingStatus.ToString(),
                Roles = new[] { RoleName.Tenant.ToString()}
            };

        }
    public async Task<VerifyEmailOtpResponse> VerifyEmailOtpAsync(
    VerifyEmailOtpRequest request,
    CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        var user = await _dbContext.Users
            .Include(x => x.UserTokens)
            .FirstOrDefaultAsync(
                x => x.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (user is null)
        {
            throw new BadRequestException(
                ErrorCodes.OtpInvalid,
                "OTP không hợp lệ.");
        }

        var otpHash = _tokenService.HashToken(request.Otp.Trim());

        var now = DateTimeOffset.UtcNow;

        var token = user.UserTokens
            .Where(x =>
                x.TokenType == TokenType.VerifyEmail &&
                x.TokenHash == otpHash &&
                x.UsedAt == null &&
                x.RevokedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (token is null)
        {
            throw new BadRequestException(
                ErrorCodes.OtpInvalid,
                "OTP không hợp lệ.");
        }

        if (token.ExpiresAt < now)
        {
            token.RevokedAt = now;
            token.RevokedReason = TokenRevokedReason.Expired;
            await _dbContext.SaveChangesAsync(cancellationToken);

            throw new BadRequestException(
                ErrorCodes.OtpExpired,
                "OTP đã hết hạn.");
        }

        user.EmailConfirmed = true;
        user.UpdatedAt = now;

        token.UsedAt = now;
        token.RevokedAt = now;
        token.RevokedReason = TokenRevokedReason.Used;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new VerifyEmailOtpResponse
        {
            UserId = user.Id,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed
        };
    }
    public async Task<ResendEmailOtpResponse> ResendEmailOtpAsync(
    ResendEmailOtpRequest request,
    CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        var user = await _dbContext.Users
            .Include(x => x.UserTokens)
            .FirstOrDefaultAsync(
                x => x.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (user is null)
        {
            return new ResendEmailOtpResponse
            {
                Email = request.Email.Trim(),
                EmailConfirmed = false,
                OtpSent = false
            };
        }

        if (user.EmailConfirmed)
        {
            return new ResendEmailOtpResponse
            {
                Email = user.Email,
                EmailConfirmed = true,
                OtpSent = false
            };
        }

        var now = DateTimeOffset.UtcNow;

        var latestVerifyEmailToken = user.UserTokens
            .Where(x => x.TokenType == TokenType.VerifyEmail)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (latestVerifyEmailToken is not null &&
            latestVerifyEmailToken.CreatedAt > now.AddSeconds(-60))
        {
            throw new TooManyRequestsException(
                ErrorCodes.OtpResendTooSoon,
                "Vui lòng chờ 60 giây trước khi gửi lại OTP.");
        }

        var activeVerifyEmailTokens = user.UserTokens
            .Where(x =>
                x.TokenType == TokenType.VerifyEmail &&
                x.UsedAt == null &&
                x.RevokedAt == null &&
                x.ExpiresAt > now)
            .ToList();

        foreach (var token in activeVerifyEmailTokens)
        {
            token.RevokedAt = now;
            token.RevokedReason = TokenRevokedReason.Replaced;
        }

        var otp = _tokenService.GenerateOtp();
        var otpHash = _tokenService.HashToken(otp);

        _dbContext.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            TokenType = TokenType.VerifyEmail,
            TokenHash = otpHash,
            ExpiresAt = now.AddMinutes(15),
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _emailSender.SendEmailVerificationOtpAsync(
            user.Email,
            user.DisplayName,
            otp,
            cancellationToken);

        return new ResendEmailOtpResponse
        {
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            OtpSent = true
        };
    }
    public async Task<LoginResponse> LoginAsync(
    LoginRequest request,
    CancellationToken cancellationToken = default)
{
    var normalizedEmail = request.Email.Trim().ToUpperInvariant();

    var user = await _dbContext.Users
        .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
        .FirstOrDefaultAsync(
            x => x.NormalizedEmail == normalizedEmail,
            cancellationToken);

    if (user is null)
    {
        _dbContext.LoginLogs.Add(new LoginLog
        {
            EmailAttempted = request.Email.Trim(),
            LoginProvider = LoginProvider.Local,
            IsSuccess = false,
            FailureReason = ErrorCodes.InvalidEmailOrPassword
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        throw new UnauthorizedException(
            ErrorCodes.InvalidEmailOrPassword,
            "Email hoặc mật khẩu không đúng.");
    }

    if (user.Status == UserStatus.Banned)
    {
        throw new ForbiddenException(
            ErrorCodes.UserBanned,
            "Tài khoản đã bị khóa bởi hệ thống.");
    }

    if (user.Status == UserStatus.Deleted)
    {
        throw new ForbiddenException(
            ErrorCodes.UserDeleted,
            "Tài khoản không còn tồn tại.");
    }

    if (user.LockoutEndAt is not null && user.LockoutEndAt > DateTimeOffset.UtcNow)
    {
        throw new ForbiddenException(
            ErrorCodes.UserLocked,
            "Tài khoản đang bị khóa tạm thời.");
    }

    if (string.IsNullOrWhiteSpace(user.PasswordHash))
    {
        throw new UnauthorizedException(
            ErrorCodes.InvalidEmailOrPassword,
            "Email hoặc mật khẩu không đúng.");
    }

    var passwordValid = _passwordService.VerifyPassword(
        user.PasswordHash,
        request.Password);

    if (!passwordValid)
    {
        user.AccessFailedCount += 1;

        if (user.AccessFailedCount >= 5)
        {
            user.LockoutEndAt = DateTimeOffset.UtcNow.AddMinutes(15);
        }

        _dbContext.LoginLogs.Add(new LoginLog
        {
            UserId = user.Id,
            EmailAttempted = request.Email.Trim(),
            LoginProvider = LoginProvider.Local,
            IsSuccess = false,
            FailureReason = ErrorCodes.InvalidEmailOrPassword
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        throw new UnauthorizedException(
            ErrorCodes.InvalidEmailOrPassword,
            "Email hoặc mật khẩu không đúng.");
    }

    if (!user.EmailConfirmed)
    {
        _dbContext.LoginLogs.Add(new LoginLog
        {
            UserId = user.Id,
            EmailAttempted = request.Email.Trim(),
            LoginProvider = LoginProvider.Local,
            IsSuccess = false,
            FailureReason = ErrorCodes.EmailVerificationRequired
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        throw new ForbiddenException(
            ErrorCodes.EmailVerificationRequired,
            "Vui lòng xác thực email trước khi đăng nhập.");
    }

    user.AccessFailedCount = 0;
    user.LockoutEndAt = null;
    var now = DateTimeOffset.UtcNow;
    user.LastLoginAt = now;

    var roles = user.UserRoles
        .Select(x => x.Role.Name.ToString())
        .ToArray();

    var accessToken = _tokenService.GenerateAccessToken(user, roles);
    var refreshToken = _tokenService.GenerateRefreshToken();

    _dbContext.LoginLogs.Add(new LoginLog
    {
        UserId = user.Id,
        EmailAttempted = request.Email.Trim(),
        LoginProvider = LoginProvider.Local,
        IsSuccess = true
    });

    _dbContext.UserTokens.Add(new UserToken
    {
        UserId = user.Id,
        TokenType = TokenType.Refresh,
        TokenHash = _tokenService.HashToken(refreshToken),
        TokenFamilyId = Guid.NewGuid(),
        ExpiresAt = now.AddDays(30),
        CreatedAt = now
    });

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new LoginResponse
    {
        UserId = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        EmailConfirmed = user.EmailConfirmed,
        Status = user.Status.ToString(),
        OnboardingStatus = user.OnboardingStatus.ToString(),
        Roles = roles,
        AccessToken = accessToken,
        RefreshToken = refreshToken
    };
}

public async Task<RefreshTokenResponse> RefreshTokenAsync(
    RefreshTokenRequest request,
    CancellationToken cancellationToken = default)
{
    var refreshToken = request.RefreshToken.Trim();
    var refreshTokenHash = _tokenService.HashToken(refreshToken);
    var now = DateTimeOffset.UtcNow;

    var token = await _dbContext.UserTokens
        .Include(x => x.User)
            .ThenInclude(x => x.UserRoles)
                .ThenInclude(x => x.Role)
        .FirstOrDefaultAsync(
            x => x.TokenType == TokenType.Refresh &&
                 x.TokenHash == refreshTokenHash,
            cancellationToken);

    if (token is null)
    {
        throw new UnauthorizedException(
            ErrorCodes.TokenInvalid,
            "Refresh token không hợp lệ.");
    }

    if (token.UsedAt is not null)
    {
        if (token.TokenFamilyId is not null)
        {
            var tokensInFamily = await _dbContext.UserTokens
                .Where(x =>
                    x.TokenType == TokenType.Refresh &&
                    x.TokenFamilyId == token.TokenFamilyId)
                .ToListAsync(cancellationToken);

            foreach (var familyToken in tokensInFamily)
            {
                familyToken.RevokedAt = now;
                familyToken.RevokedReason = TokenRevokedReason.ReuseDetected;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        throw new UnauthorizedException(
            ErrorCodes.RefreshTokenReuseDetected,
            "Refresh token đã được sử dụng lại.");
    }

    if (token.RevokedAt is not null)
    {
        throw new UnauthorizedException(
            ErrorCodes.TokenInvalid,
            "Refresh token không hợp lệ.");
    }

    if (token.ExpiresAt <= now)
    {
        token.RevokedAt = now;
        token.RevokedReason = TokenRevokedReason.Expired;
        await _dbContext.SaveChangesAsync(cancellationToken);

        throw new UnauthorizedException(
            ErrorCodes.TokenExpired,
            "Refresh token đã hết hạn.");
    }

    var user = token.User;

    if (user.Status is UserStatus.Banned or UserStatus.Deleted)
    {
        throw new UnauthorizedException(
            ErrorCodes.TokenInvalid,
            "Refresh token không hợp lệ.");
    }

    var roles = user.UserRoles
        .Select(x => x.Role.Name.ToString())
        .ToArray();

    var newAccessToken = _tokenService.GenerateAccessToken(user, roles);
    var newRefreshToken = _tokenService.GenerateRefreshToken();
    var newUserToken = new UserToken
    {
        UserId = user.Id,
        TokenType = TokenType.Refresh,
        TokenHash = _tokenService.HashToken(newRefreshToken),
        TokenFamilyId = token.TokenFamilyId ?? Guid.NewGuid(),
        ExpiresAt = now.AddDays(30),
        CreatedAt = now
    };

    token.UsedAt = now;
    token.RevokedAt = now;
    token.RevokedReason = TokenRevokedReason.TokenRotated;
    token.ReplacedByTokenId = newUserToken.Id;

    _dbContext.UserTokens.Add(newUserToken);

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new RefreshTokenResponse
    {
        AccessToken = newAccessToken,
        RefreshToken = newRefreshToken
    };
}

public async Task<LogoutResponse> LogoutAsync(
    LogoutRequest request,
    CancellationToken cancellationToken = default)
{
    var refreshToken = request.RefreshToken.Trim();
    var refreshTokenHash = _tokenService.HashToken(refreshToken);
    var now = DateTimeOffset.UtcNow;

    var token = await _dbContext.UserTokens
        .FirstOrDefaultAsync(
            x => x.TokenType == TokenType.Refresh &&
                 x.TokenHash == refreshTokenHash,
            cancellationToken);

    if (token is null || token.UsedAt is not null || token.RevokedAt is not null)
    {
        throw new UnauthorizedException(
            ErrorCodes.TokenInvalid,
            "Refresh token không hợp lệ.");
    }

    if (token.ExpiresAt <= now)
    {
        token.RevokedAt = now;
        token.RevokedReason = TokenRevokedReason.Expired;
        await _dbContext.SaveChangesAsync(cancellationToken);

        throw new UnauthorizedException(
            ErrorCodes.TokenExpired,
            "Refresh token đã hết hạn.");
    }

    token.RevokedAt = now;
    token.RevokedReason = TokenRevokedReason.Logout;

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new LogoutResponse
    {
        RevokedTokenCount = 1
    };
}

public async Task<LogoutResponse> LogoutAllAsync(
    CancellationToken cancellationToken = default)
{
    if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
    {
        throw new UnauthorizedException(
            ErrorCodes.Unauthorized,
            "Bạn cần đăng nhập để thực hiện thao tác này.");
    }

    var now = DateTimeOffset.UtcNow;
    var userId = _currentUserService.UserId.Value;

    var activeRefreshTokens = await _dbContext.UserTokens
        .Where(x =>
            x.UserId == userId &&
            x.TokenType == TokenType.Refresh &&
            x.UsedAt == null &&
            x.RevokedAt == null &&
            x.ExpiresAt > now)
        .ToListAsync(cancellationToken);

    foreach (var token in activeRefreshTokens)
    {
        token.RevokedAt = now;
        token.RevokedReason = TokenRevokedReason.LogoutAllDevices;
    }

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new LogoutResponse
    {
        RevokedTokenCount = activeRefreshTokens.Count
    };
}

public async Task<ForgotPasswordResponse> ForgotPasswordAsync(
    ForgotPasswordRequest request,
    CancellationToken cancellationToken = default)
{
    var normalizedEmail = request.Email.Trim().ToUpperInvariant();
    var user = await _dbContext.Users
        .Include(x => x.UserTokens)
        .FirstOrDefaultAsync(
            x => x.NormalizedEmail == normalizedEmail,
            cancellationToken);

    if (user is null)
    {
        return new ForgotPasswordResponse
        {
            Email = request.Email.Trim()
        };
    }

    var now = DateTimeOffset.UtcNow;

    var activeResetTokens = user.UserTokens
        .Where(x =>
            x.TokenType == TokenType.ResetPassword &&
            x.UsedAt == null &&
            x.RevokedAt == null &&
            x.ExpiresAt > now)
        .ToList();

    foreach (var token in activeResetTokens)
    {
        token.RevokedAt = now;
        token.RevokedReason = TokenRevokedReason.Replaced;
    }

    var otp = _tokenService.GenerateOtp();

    _dbContext.UserTokens.Add(new UserToken
    {
        UserId = user.Id,
        TokenType = TokenType.ResetPassword,
        TokenHash = _tokenService.HashToken(otp),
        ExpiresAt = now.AddMinutes(15),
        CreatedAt = now
    });

    await _dbContext.SaveChangesAsync(cancellationToken);

    await _emailSender.SendResetPasswordOtpAsync(
        user.Email,
        user.DisplayName,
        otp,
        cancellationToken);

    return new ForgotPasswordResponse
    {
        Email = request.Email.Trim()
    };
}

public async Task<ResetPasswordResponse> ResetPasswordAsync(
    ResetPasswordRequest request,
    CancellationToken cancellationToken = default)
{
    var normalizedEmail = request.Email.Trim().ToUpperInvariant();
    var user = await _dbContext.Users
        .Include(x => x.UserTokens)
        .FirstOrDefaultAsync(
            x => x.NormalizedEmail == normalizedEmail,
            cancellationToken);

    if (user is null)
    {
        throw new BadRequestException(
            ErrorCodes.OtpInvalid,
            "OTP không hợp lệ.");
    }

    var now = DateTimeOffset.UtcNow;
    var otpHash = _tokenService.HashToken(request.Otp.Trim());

    var token = user.UserTokens
        .Where(x =>
            x.TokenType == TokenType.ResetPassword &&
            x.TokenHash == otpHash &&
            x.UsedAt == null &&
            x.RevokedAt == null)
        .OrderByDescending(x => x.CreatedAt)
        .FirstOrDefault();

    if (token is null)
    {
        throw new BadRequestException(
            ErrorCodes.OtpInvalid,
            "OTP không hợp lệ.");
    }

    if (token.ExpiresAt <= now)
    {
        token.RevokedAt = now;
        token.RevokedReason = TokenRevokedReason.Expired;
        await _dbContext.SaveChangesAsync(cancellationToken);

        throw new BadRequestException(
            ErrorCodes.OtpExpired,
            "OTP đã hết hạn.");
    }

    user.PasswordHash = _passwordService.HashPassword(request.NewPassword);
    user.EmailConfirmed = true;
    user.AccessFailedCount = 0;
    user.LockoutEndAt = null;
    user.UpdatedAt = now;

    token.UsedAt = now;
    token.RevokedAt = now;
    token.RevokedReason = TokenRevokedReason.Used;

    var activeRefreshTokens = await _dbContext.UserTokens
        .Where(x =>
            x.UserId == user.Id &&
            x.TokenType == TokenType.Refresh &&
            x.UsedAt == null &&
            x.RevokedAt == null &&
            x.ExpiresAt > now)
        .ToListAsync(cancellationToken);

    foreach (var refreshToken in activeRefreshTokens)
    {
        refreshToken.RevokedAt = now;
        refreshToken.RevokedReason = TokenRevokedReason.PasswordChanged;
    }

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new ResetPasswordResponse
    {
        Email = user.Email,
        EmailConfirmed = user.EmailConfirmed,
        RevokedRefreshTokenCount = activeRefreshTokens.Count
    };
}

public async Task<GoogleLoginResponse> GoogleLoginAsync(
    GoogleLoginRequest request,
    CancellationToken cancellationToken = default)
{
    var googleUser = await _googleAuthService.VerifyIdTokenAsync(
        request.IdToken.Trim(),
        cancellationToken);

    var normalizedEmail = googleUser.Email.Trim().ToUpperInvariant();
    var now = DateTimeOffset.UtcNow;

    var externalLogin = await _dbContext.ExternalLogins
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
    }
    else
    {
        user = await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.NormalizedEmail == normalizedEmail,
                cancellationToken)
            ?? await CreateGoogleUserAsync(googleUser, now, cancellationToken);

        _dbContext.ExternalLogins.Add(new ExternalLogin
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

    if (user.Status == UserStatus.Banned)
    {
        throw new ForbiddenException(
            ErrorCodes.UserBanned,
            "Tài khoản đã bị khóa bởi hệ thống.");
    }

    if (user.Status == UserStatus.Deleted)
    {
        throw new ForbiddenException(
            ErrorCodes.UserDeleted,
            "Tài khoản không còn tồn tại.");
    }

    if (user.LockoutEndAt is not null && user.LockoutEndAt > now)
    {
        throw new ForbiddenException(
            ErrorCodes.UserLocked,
            "Tài khoản đang bị khóa tạm thời.");
    }

    if (!user.EmailConfirmed)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildGoogleLoginResponse(
            user,
            roles: user.UserRoles.Select(x => x.Role.Name.ToString()).ToArray(),
            requiresEmailVerification: true,
            accessToken: null,
            refreshToken: null);
    }

    var roles = user.UserRoles
        .Select(x => x.Role.Name.ToString())
        .ToArray();

    var accessToken = _tokenService.GenerateAccessToken(user, roles);
    var refreshToken = _tokenService.GenerateRefreshToken();

    user.LastLoginAt = now;

    _dbContext.LoginLogs.Add(new LoginLog
    {
        UserId = user.Id,
        EmailAttempted = googleUser.Email.Trim(),
        LoginProvider = LoginProvider.Google,
        IsSuccess = true
    });

    _dbContext.UserTokens.Add(new UserToken
    {
        UserId = user.Id,
        TokenType = TokenType.Refresh,
        TokenHash = _tokenService.HashToken(refreshToken),
        TokenFamilyId = Guid.NewGuid(),
        ExpiresAt = now.AddDays(30),
        CreatedAt = now
    });

    await _dbContext.SaveChangesAsync(cancellationToken);

    return BuildGoogleLoginResponse(
        user,
        roles,
        requiresEmailVerification: false,
        accessToken,
        refreshToken);
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
        CreatedAt = now
    };

    var tenantRole = await _dbContext.Roles.FirstAsync(
        x => x.Name == RoleName.Tenant,
        cancellationToken);

    user.UserRoles.Add(new UserRole
    {
        UserId = user.Id,
        RoleId = tenantRole.Id,
        Role = tenantRole
    });

    _dbContext.Users.Add(user);

    return user;
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
        EmailConfirmed = user.EmailConfirmed,
        RequiresEmailVerification = requiresEmailVerification,
        Status = user.Status.ToString(),
        OnboardingStatus = user.OnboardingStatus.ToString(),
        Roles = roles,
        AccessToken = accessToken,
        RefreshToken = refreshToken
    };
}
}
