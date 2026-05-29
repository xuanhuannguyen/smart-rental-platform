using SmartRentalPlatform.Contracts.Auth;

namespace SmartRentalPlatform.Application.Auth;

public interface IGoogleLoginService
{
    Task<GoogleLoginResponse> GoogleLoginAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default);
}
