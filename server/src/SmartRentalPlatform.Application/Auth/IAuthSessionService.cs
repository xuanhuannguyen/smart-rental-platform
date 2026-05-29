using SmartRentalPlatform.Contracts.Auth;

namespace SmartRentalPlatform.Application.Auth;

public interface IAuthSessionService
{
    Task<RefreshTokenResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<LogoutResponse> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default);

    Task<LogoutResponse> LogoutAllAsync(
        CancellationToken cancellationToken = default);
}
