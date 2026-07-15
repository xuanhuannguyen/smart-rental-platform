using System;

namespace SmartRentalPlatform.Contracts.Billing.Responses;

public sealed record AdminBillingServiceTypeResponse(
    Guid Id,
    string Name,
    bool SupportsMeterReading,
    string? MeterUnitName,
    bool IsActive,
    DateTimeOffset CreatedAt
);
