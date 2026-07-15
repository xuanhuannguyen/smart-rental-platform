namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IPayOSWebhookSignatureVerifier
{
    bool Verify(string rawPayload, string? signatureHeader);
    bool VerifyPayment(string rawPayload, string? signatureHeader);
    bool VerifyPayout(string rawPayload, string? signatureHeader);
}
