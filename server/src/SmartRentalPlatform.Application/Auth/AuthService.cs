using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Auth;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IAppDbContext _dbContext;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(
        IAppDbContext dbContext,
        IPasswordService passwordService,
        ITokenService tokenService,
        IEmailSender emailSender,
        IHttpContextAccessor httpContextAccessor
    )
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _httpContextAccessor = httpContextAccessor;
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
                TokenFamilyId = Guid.NewGuid(),
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
            TokenFamilyId = Guid.NewGuid(),
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
        CreatedAt = now,
        CreatedByIp = GetCurrentIp(),
        UserAgent = GetCurrentUserAgent()
    });

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new LoginResponse
    {
        UserId = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        AvatarUrl = user.AvatarUrl,
        AvatarMediaAssetId = user.AvatarMediaAssetId,
        IsGoogleUser = string.IsNullOrEmpty(user.PasswordHash),
        EmailConfirmed = user.EmailConfirmed,
        Status = user.Status.ToString(),
        OnboardingStatus = user.OnboardingStatus.ToString(),
        Roles = roles,
        AccessToken = accessToken,
        RefreshToken = refreshToken
    };
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
}
