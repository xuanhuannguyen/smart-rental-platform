using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartRentalPlatform.Application.Auth;
using SmartRentalPlatform.Contracts.Auth;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAuthSessionService _authSessionService;
    private readonly IAuthPasswordService _authPasswordService;
    private readonly IGoogleLoginService _googleLoginService;

    public AuthController(
        IAuthService authService,
        IAuthSessionService authSessionService,
        IAuthPasswordService authPasswordService,
        IGoogleLoginService googleLoginService)
    {
        _authService = authService;
        _authSessionService = authSessionService;
        _authPasswordService = authPasswordService;
        _googleLoginService = googleLoginService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<RegisterResponse>>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);

        return Ok(new ApiResponse<RegisterResponse>
        {
            Success = true,
            Message = "OTP xác thực email đã được gửi.",
            Data = result
        });
    }
    [HttpPost("verify-email-otp")]
    public async Task<ActionResult<ApiResponse<VerifyEmailOtpResponse>>> VerifyEmailOtp(
     VerifyEmailOtpRequest request,
     CancellationToken cancellationToken)
    {
        var result = await _authService.VerifyEmailOtpAsync(request, cancellationToken);

        return Ok(new ApiResponse<VerifyEmailOtpResponse>
        {
            Success = true,
            Message = "Xác thực email thành công.",
            Data = result
        });
    }
    [HttpPost("resend-email-otp")]
    public async Task<ActionResult<ApiResponse<ResendEmailOtpResponse>>> ResendEmailOtp(
    ResendEmailOtpRequest request,
    CancellationToken cancellationToken)
    {
        var result = await _authService.ResendEmailOtpAsync(request, cancellationToken);

        return Ok(new ApiResponse<ResendEmailOtpResponse>
        {
            Success = true,
            Message = result.OtpSent
                ? "OTP xác thực email mới đã được gửi."
                : "Nếu email hợp lệ, hệ thống sẽ xử lý yêu cầu gửi lại OTP.",
            Data = result
        });
    }
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);

    return Ok(new ApiResponse<LoginResponse>
    {
        Success = true,
        Message = "Đăng nhập thành công.",
        Data = result
    });
}

    [HttpPost("refresh-token")]
    public async Task<ActionResult<ApiResponse<RefreshTokenResponse>>> RefreshToken(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authSessionService.RefreshTokenAsync(request, cancellationToken);

        return Ok(new ApiResponse<RefreshTokenResponse>
        {
            Success = true,
            Message = "Làm mới token thành công.",
            Data = result
        });
    }

    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<LogoutResponse>>> Logout(
        LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authSessionService.LogoutAsync(request, cancellationToken);

        return Ok(new ApiResponse<LogoutResponse>
        {
            Success = true,
            Message = "Đăng xuất thiết bị hiện tại thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<ActionResult<ApiResponse<LogoutResponse>>> LogoutAll(
        CancellationToken cancellationToken)
    {
        var result = await _authSessionService.LogoutAllAsync(cancellationToken);

        return Ok(new ApiResponse<LogoutResponse>
        {
            Success = true,
            Message = "Đăng xuất tất cả thiết bị thành công.",
            Data = result
        });
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<ForgotPasswordResponse>>> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authPasswordService.ForgotPasswordAsync(request, cancellationToken);

        return Ok(new ApiResponse<ForgotPasswordResponse>
        {
            Success = true,
            Message = "Nếu email tồn tại, hệ thống đã gửi OTP đặt lại mật khẩu.",
            Data = result
        });
    }

    [HttpPost("verify-reset-otp")]
    public async Task<ActionResult<ApiResponse<VerifyResetOtpResponse>>> VerifyResetOtp(
        VerifyResetOtpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authPasswordService.VerifyResetOtpAsync(request, cancellationToken);

        return Ok(new ApiResponse<VerifyResetOtpResponse>
        {
            Success = true,
            Message = "OTP hợp lệ.",
            Data = result
        });
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<ResetPasswordResponse>>> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authPasswordService.ResetPasswordAsync(request, cancellationToken);

        return Ok(new ApiResponse<ResetPasswordResponse>
        {
            Success = true,
            Message = "Đặt lại mật khẩu thành công.",
            Data = result
        });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<ChangePasswordResponse>>> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authPasswordService.ChangePasswordAsync(request, cancellationToken);

        return Ok(new ApiResponse<ChangePasswordResponse>
        {
            Success = true,
            Message = "Đổi mật khẩu thành công.",
            Data = result
        });
    }

    [HttpPost("google-login")]
    public async Task<ActionResult<ApiResponse<GoogleLoginResponse>>> GoogleLogin(
        GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _googleLoginService.GoogleLoginAsync(request, cancellationToken);

        return Ok(new ApiResponse<GoogleLoginResponse>
        {
            Success = true,
            Message = result.RequiresEmailVerification
                ? "Vui lòng xác thực email trước khi tiếp tục."
                : "Đăng nhập Google thành công.",
            Data = result
        });
    }
}
