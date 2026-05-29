using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Auth;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Auth;

public class AuthPasswordService : IAuthPasswordService
{
    private readonly IAppDbContext dbContext;
    private readonly IPasswordService passwordService;
    private readonly ITokenService tokenService;
    private readonly IEmailSender emailSender;
    private readonly ICurrentUserService currentUserService;

    public AuthPasswordService(
        IAppDbContext dbContext,
        IPasswordService passwordService,
        ITokenService tokenService,
        IEmailSender emailSender,
        ICurrentUserService currentUserService)
    {
        this.dbContext = dbContext;
        this.passwordService = passwordService;
        this.tokenService = tokenService;
        this.emailSender = emailSender;
        this.currentUserService = currentUserService;
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await dbContext.Users
            .Include(x => x.UserTokens)
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return new ForgotPasswordResponse { Email = request.Email.Trim() };
        }

        var now = DateTimeOffset.UtcNow;
        RevokeActiveResetTokens(user, now);

        var otp = tokenService.GenerateOtp();
        dbContext.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            TokenType = TokenType.ResetPassword,
            TokenHash = tokenService.HashToken(otp),
            TokenFamilyId = Guid.NewGuid(),
            ExpiresAt = now.AddMinutes(15),
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await emailSender.SendResetPasswordOtpAsync(
            user.Email,
            user.DisplayName,
            otp,
            cancellationToken);

        return new ForgotPasswordResponse { Email = request.Email.Trim() };
    }

    public async Task<VerifyResetOtpResponse> VerifyResetOtpAsync(
        VerifyResetOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserWithTokensAsync(request.Email, cancellationToken);
        var token = await GetValidResetOtpTokenAsync(user, request.Otp, cancellationToken);
        return new VerifyResetOtpResponse { Valid = token is not null };
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserWithTokensAsync(request.Email, cancellationToken);
        var token = await GetValidResetOtpTokenAsync(user, request.Otp, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        user.PasswordHash = passwordService.HashPassword(request.NewPassword);
        user.EmailConfirmed = true;
        user.AccessFailedCount = 0;
        user.LockoutEndAt = null;
        user.UpdatedAt = now;

        token.UsedAt = now;
        token.RevokedAt = now;
        token.RevokedReason = TokenRevokedReason.Used;

        var activeRefreshTokens = await RevokeActiveRefreshTokensAsync(user.Id, TokenRevokedReason.PasswordChanged, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ResetPasswordResponse
        {
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            RevokedRefreshTokenCount = activeRefreshTokens
        };
    }

    public async Task<ChangePasswordResponse> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            throw new UnauthorizedException(ErrorCodes.Unauthorized, "Bạn cần đăng nhập để thực hiện thao tác này.");
        }

        var userId = currentUserService.UserId.Value;
        var now = DateTimeOffset.UtcNow;

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedException(ErrorCodes.Unauthorized, "Token không còn hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            throw new BadRequestException(ErrorCodes.InvalidEmailOrPassword, "Tài khoản này chưa có mật khẩu cục bộ.");
        }

        var currentPasswordValid = passwordService.VerifyPassword(user.PasswordHash, request.CurrentPassword);
        if (!currentPasswordValid)
        {
            throw new BadRequestException(ErrorCodes.InvalidEmailOrPassword, "Mật khẩu hiện tại không đúng.");
        }

        user.PasswordHash = passwordService.HashPassword(request.NewPassword);
        user.UpdatedAt = now;

        var revokedCount = await RevokeActiveRefreshTokensAsync(user.Id, TokenRevokedReason.PasswordChanged, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ChangePasswordResponse
        {
            PasswordChanged = true,
            RevokedRefreshTokenCount = revokedCount
        };
    }

    private async Task<User> FindUserWithTokensAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.Users
            .Include(x => x.UserTokens)
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            throw new BadRequestException(ErrorCodes.OtpInvalid, "OTP không hợp lệ.");
        }

        return user;
    }

    private async Task<UserToken> GetValidResetOtpTokenAsync(
        User user,
        string otp,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var otpHash = tokenService.HashToken(otp.Trim());

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
            throw new BadRequestException(ErrorCodes.OtpInvalid, "OTP không hợp lệ.");
        }

        if (token.ExpiresAt <= now)
        {
            token.RevokedAt = now;
            token.RevokedReason = TokenRevokedReason.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new BadRequestException(ErrorCodes.OtpExpired, "OTP đã hết hạn.");
        }

        return token;
    }

    private static void RevokeActiveResetTokens(User user, DateTimeOffset now)
    {
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
    }

    private async Task<int> RevokeActiveRefreshTokensAsync(
        Guid userId,
        TokenRevokedReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeRefreshTokens = await dbContext.UserTokens
            .Where(x =>
                x.UserId == userId &&
                x.TokenType == TokenType.Refresh &&
                x.UsedAt == null &&
                x.RevokedAt == null &&
                x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.RevokedAt = now;
            refreshToken.RevokedReason = reason;
        }

        return activeRefreshTokens.Count;
    }
}
