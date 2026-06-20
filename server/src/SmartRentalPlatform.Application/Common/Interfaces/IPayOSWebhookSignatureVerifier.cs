namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IPayOSWebhookSignatureVerifier
{
    bool Verify(string rawPayload, string? signatureHeader);
}
