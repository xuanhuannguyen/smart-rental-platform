using System;

namespace SmartRentalPlatform.Contracts.RoomingHouses.Responses;

public class RoomingHouseServicePriceResponse
{
    public Guid Id { get; set; }
    public Guid ServiceTypeId { get; set; }
    public string ServiceTypeName { get; set; } = string.Empty;
    public string PricingUnit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string? Note { get; set; }
    public string? MeterUnitName { get; set; }
    public bool IsActive { get; set; }
}
