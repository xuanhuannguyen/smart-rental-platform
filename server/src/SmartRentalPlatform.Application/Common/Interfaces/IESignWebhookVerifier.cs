namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IESignWebhookVerifier
{
    bool VerifySignature(string? signatureHeader, string rawPayload);
}
