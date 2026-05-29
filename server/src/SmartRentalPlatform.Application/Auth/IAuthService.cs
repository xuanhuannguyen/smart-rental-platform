using SmartRentalPlatform.Contracts.Auth;

namespace SmartRentalPlatform.Application.Auth;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default
    );
    Task<VerifyEmailOtpResponse> VerifyEmailOtpAsync(
        VerifyEmailOtpRequest request,
        CancellationToken cancellationToken = default
    );
    Task<ResendEmailOtpResponse> ResendEmailOtpAsync(
        ResendEmailOtpRequest request,
        CancellationToken cancellationToken = default);
    Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

}
