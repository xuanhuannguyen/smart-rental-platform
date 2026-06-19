namespace SmartRentalPlatform.Contracts.Billing.Responses;

public sealed record ServicePriceResponse(
    Guid Id,
    Guid RoomingHouseId,
    Guid ServiceTypeId,
    string ServiceCode,
    string ServiceName,
    string BillingMethod,
    string UnitName,
    decimal UnitPrice,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
