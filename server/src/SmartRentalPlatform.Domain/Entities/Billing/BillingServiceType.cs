using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Domain.Entities.Billing;

public class BillingServiceType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool SupportsMeterReading { get; set; }
    public string? MeterUnitName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<RoomingHouseServicePrice> RoomingHouseServicePrices { get; set; } = new List<RoomingHouseServicePrice>();
    public ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();
    public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
}
