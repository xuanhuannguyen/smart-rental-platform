namespace SmartRentalPlatform.Contracts.Billing.Responses;

public sealed record BillingServiceTypeResponse(
    Guid Id,
    string Name,
    bool SupportsMeterReading,
    string? MeterUnitName,
    bool IsActive);
