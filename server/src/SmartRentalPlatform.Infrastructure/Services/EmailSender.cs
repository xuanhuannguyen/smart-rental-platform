using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.Common.Interfaces;

namespace SmartRentalPlatform.Infrastructure.Services;

public class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(ILogger<EmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailVerificationOtpAsync(
        string email,
        string displayName,
        string otp,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Email verification OTP for {Email} ({DisplayName}): {Otp}",
            email,
            displayName,
            otp);

        return Task.CompletedTask;
    }

    public Task SendResetPasswordOtpAsync(
        string email,
        string displayName,
        string otp,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Reset password OTP for {Email} ({DisplayName}): {Otp}",
            email,
            displayName,
            otp);

        return Task.CompletedTask;
    }
}