namespace SmartRentalPlatform.Application.Billing;

public interface IInvoiceWalletPaymentService
{
    Task<InvoiceWalletPaymentResult> PayInvoiceAsync(
        Guid invoiceId,
        Guid tenantUserId,
        CancellationToken cancellationToken = default);
}

public sealed record InvoiceWalletPaymentResult(
    bool Success,
    Guid? TransferGroupId,
    string? ErrorMessage);
