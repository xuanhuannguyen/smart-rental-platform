namespace SmartRentalPlatform.Contracts.Billing.Responses;

public sealed record InvoicePaymentResponse(
    Guid Id,
    Guid InvoiceId,
    decimal Amount,
    Guid WalletTransferGroupId,
    string Status,
    DateTimeOffset PaidAt);
