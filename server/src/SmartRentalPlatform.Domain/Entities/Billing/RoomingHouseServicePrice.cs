using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Domain.Entities.Billing;

public class RoomingHouseServicePrice
{
    public Guid Id { get; set; }
    public Guid RoomingHouseId { get; set; }
    public Guid ServiceTypeId { get; set; }
    public PricingUnit PricingUnit { get; set; }
    public decimal UnitPrice { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public RoomingHouse RoomingHouse { get; set; } = null!;
    public BillingServiceType ServiceType { get; set; } = null!;
}
