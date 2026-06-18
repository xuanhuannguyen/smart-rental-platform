namespace SmartRentalPlatform.Application.Billing;

public interface IInvoiceWalletPaymentService
{
    Task<InvoiceWalletPaymentResult> PayInvoiceAsync(
        Guid invoiceId,
        Guid tenantUserId,
        Guid landlordUserId,
        decimal amount,
        CancellationToken cancellationToken = default);
}

public sealed record InvoiceWalletPaymentResult(
    bool Success,
    Guid? TransferGroupId,
    string? ErrorMessage);
