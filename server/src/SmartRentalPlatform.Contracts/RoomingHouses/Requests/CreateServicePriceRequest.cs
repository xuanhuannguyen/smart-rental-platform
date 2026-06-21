namespace SmartRentalPlatform.Contracts.RoomingHouses.Requests;

public sealed record CreateServicePriceRequest(
    Guid ServiceTypeId,
    string PricingUnit,
    decimal UnitPrice,
    DateOnly EffectiveFrom,
    string? Note);
