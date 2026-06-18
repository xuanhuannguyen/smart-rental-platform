namespace SmartRentalPlatform.Contracts.Billing.Requests;

public sealed record CreateServicePriceRequest(
    string ServiceCode,
    string BillingMethod,
    string UnitName,
    decimal UnitPrice,
    DateOnly EffectiveFrom,
    string? Note);
