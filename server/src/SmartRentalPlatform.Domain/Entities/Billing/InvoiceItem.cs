namespace SmartRentalPlatform.Domain.Entities.Billing;

public class InvoiceItem
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid? ServiceTypeId { get; set; }
    public Guid? MeterReadingId { get; set; }
    public InvoiceItemType ItemType { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Invoice Invoice { get; set; } = null!;
    public BillingServiceType? ServiceType { get; set; }
    public MeterReading? MeterReading { get; set; }
}
