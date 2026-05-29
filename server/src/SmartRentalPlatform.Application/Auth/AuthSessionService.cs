using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Auth;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Auth;

public class AuthSessionService : IAuthSessionService
{
    private readonly IAppDbContext dbContext;
    private readonly ITokenService tokenService;
    private readonly ICurrentUserService currentUserService;
    private readonly IHttpContextAccessor httpContextAccessor;

    public AuthSessionService(
        IAppDbContext dbContext,
        ITokenService tokenService,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor)
    {
        this.dbContext = dbContext;
        this.tokenService = tokenService;
        this.currentUserService = currentUserService;
        this.httpContextAccessor = httpContextAccessor;
    }

    public async Task<RefreshTokenResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = request.RefreshToken.Trim();
        var refreshTokenHash = tokenService.HashToken(refreshToken);
        var now = DateTimeOffset.UtcNow;

        var token = await dbContext.UserTokens
            .Include(x => x.User)
                .ThenInclude(x => x.UserRoles)
                    .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.TokenType == TokenType.Refresh &&
                     x.TokenHash == refreshTokenHash,
                cancellationToken);

        if (token is null)
        {
            throw new UnauthorizedException(ErrorCodes.TokenInvalid, "Refresh token không hợp lệ.");
        }

        if (token.UsedAt is not null)
        {
            await RevokeTokenFamilyOnReuseAsync(token, now, cancellationToken);
            throw new UnauthorizedException(ErrorCodes.RefreshTokenReuseDetected, "Refresh token đã được sử dụng lại.");
        }

        if (token.RevokedAt is not null)
        {
            throw new UnauthorizedException(ErrorCodes.TokenInvalid, "Refresh token không hợp lệ.");
        }

        if (token.ExpiresAt <= now)
        {
            token.RevokedAt = now;
            token.RevokedReason = TokenRevokedReason.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException(ErrorCodes.TokenExpired, "Refresh token đã hết hạn.");
        }

        var user = token.User;

        if (user.Status is UserStatus.Banned or UserStatus.Deleted)
        {
            throw new UnauthorizedException(ErrorCodes.TokenInvalid, "Refresh token không hợp lệ.");
        }

        var roles = user.UserRoles
            .Select(x => x.Role.Name.ToString())
            .ToArray();

        var newAccessToken = tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = tokenService.GenerateRefreshToken();
        var newUserToken = new UserToken
        {
            UserId = user.Id,
            TokenType = TokenType.Refresh,
            TokenHash = tokenService.HashToken(newRefreshToken),
            TokenFamilyId = token.TokenFamilyId ?? Guid.NewGuid(),
            ExpiresAt = now.AddDays(30),
            CreatedAt = now,
            CreatedByIp = GetCurrentIp(),
            UserAgent = GetCurrentUserAgent()
        };

        token.UsedAt = now;
        token.RevokedAt = now;
        token.RevokedReason = TokenRevokedReason.TokenRotated;
        token.ReplacedByTokenId = newUserToken.Id;

        dbContext.UserTokens.Add(newUserToken);
        await dbContext.SaveChangesAsync(cancellationToken);

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
        var refreshTokenHash = tokenService.HashToken(refreshToken);
        var now = DateTimeOffset.UtcNow;

        var token = await dbContext.UserTokens
            .FirstOrDefaultAsync(
                x => x.TokenType == TokenType.Refresh &&
                     x.TokenHash == refreshTokenHash,
                cancellationToken);

        if (token is null || token.UsedAt is not null || token.RevokedAt is not null)
        {
            throw new UnauthorizedException(ErrorCodes.TokenInvalid, "Refresh token không hợp lệ.");
        }

        if (token.ExpiresAt <= now)
        {
            token.RevokedAt = now;
            token.RevokedReason = TokenRevokedReason.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException(ErrorCodes.TokenExpired, "Refresh token đã hết hạn.");
        }

        token.RevokedAt = now;
        token.RevokedReason = TokenRevokedReason.Logout;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new LogoutResponse
        {
            RevokedTokenCount = 1
        };
    }

    public async Task<LogoutResponse> LogoutAllAsync(
        CancellationToken cancellationToken = default)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
        {
            throw new UnauthorizedException(ErrorCodes.Unauthorized, "Bạn cần đăng nhập để thực hiện thao tác này.");
        }

        var now = DateTimeOffset.UtcNow;
        var userId = currentUserService.UserId.Value;

        var activeRefreshTokens = await dbContext.UserTokens
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

        await dbContext.SaveChangesAsync(cancellationToken);

        return new LogoutResponse
        {
            RevokedTokenCount = activeRefreshTokens.Count
        };
    }

    private async Task RevokeTokenFamilyOnReuseAsync(
        UserToken token,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (token.TokenFamilyId is null)
        {
            return;
        }

        var tokensInFamily = await dbContext.UserTokens
            .Where(x =>
                x.TokenType == TokenType.Refresh &&
                x.TokenFamilyId == token.TokenFamilyId)
            .ToListAsync(cancellationToken);

        foreach (var familyToken in tokensInFamily)
        {
            familyToken.RevokedAt = now;
            familyToken.RevokedReason = TokenRevokedReason.ReuseDetected;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private string? GetCurrentIp()
    {
        var context = httpContextAccessor.HttpContext;
        var ip = context?.Connection?.RemoteIpAddress?.ToString();
        return ip == "::1" ? "127.0.0.1" : ip;
    }

    private string? GetCurrentUserAgent()
    {
        return httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString();
    }
}
