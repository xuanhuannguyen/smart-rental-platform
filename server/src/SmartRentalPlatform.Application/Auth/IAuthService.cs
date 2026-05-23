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

    Task<RefreshTokenResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<LogoutResponse> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default);

    Task<LogoutResponse> LogoutAllAsync(
        CancellationToken cancellationToken = default);

    Task<ForgotPasswordResponse> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<ResetPasswordResponse> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<GoogleLoginResponse> GoogleLoginAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default);
}
