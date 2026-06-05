namespace SmartRentalPlatform.Contracts.Billing.Requests;

public sealed record GenerateInvoiceDraftRequest(
    Guid ContractId,
    DateOnly BillingPeriodStart,
    DateOnly BillingPeriodEnd,
    decimal DiscountAmount,
    string? Note);
