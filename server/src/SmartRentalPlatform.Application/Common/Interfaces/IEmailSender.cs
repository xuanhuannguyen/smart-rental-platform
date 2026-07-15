namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendEmailVerificationOtpAsync(
        string email,
        string displayName,
        string otp,
        CancellationToken cancellationToken = default);

    Task SendResetPasswordOtpAsync(
        string email,
        string displayName,
        string otp,
        CancellationToken cancellationToken = default);

    Task SendContractSignatureOtpAsync(
        string email,
        string displayName,
        string contractNumber,
        string role,
        string otp,
        CancellationToken cancellationToken = default);
}
