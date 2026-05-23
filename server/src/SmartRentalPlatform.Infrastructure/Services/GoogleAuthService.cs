using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Infrastructure.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly IConfiguration _configuration;

    public GoogleAuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<GoogleUserInfo> VerifyIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var clientId = _configuration["Authentication:Google:ClientId"];
            var settings = string.IsNullOrWhiteSpace(clientId)
                ? new GoogleJsonWebSignature.ValidationSettings()
                : new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            if (!payload.EmailVerified)
            {
                throw new UnauthorizedException(
                    ErrorCodes.GoogleIdTokenInvalid,
                    "Google email chưa được xác thực.");
            }

            return new GoogleUserInfo
            {
                ProviderUserId = payload.Subject,
                Email = payload.Email,
                DisplayName = payload.Name,
                AvatarUrl = payload.Picture,
                EmailVerified = payload.EmailVerified
            };
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedException(
                ErrorCodes.GoogleIdTokenInvalid,
                "Google idToken không hợp lệ.");
        }
    }
}
