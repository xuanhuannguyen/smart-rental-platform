namespace SmartRentalPlatform.Contracts.RoomingHouses.Responses;

public sealed record ServicePriceResponse(
    Guid Id,
    Guid RoomingHouseId,
    Guid ServiceTypeId,
    string ServiceName,
    bool SupportsMeterReading,
    string? MeterUnitName,
    string PricingUnit,
    string DisplayUnitName,
    decimal UnitPrice,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
