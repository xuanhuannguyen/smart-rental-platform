namespace SmartRentalPlatform.Application.Wallets;

public interface IWithdrawalWebhookService
{
    Task ProcessWebhookAsync(
        string providerOrderCode,
        string status,
        string payload,
        string? signature,
        bool skipSignatureVerification = false,
        CancellationToken cancellationToken = default);
}
