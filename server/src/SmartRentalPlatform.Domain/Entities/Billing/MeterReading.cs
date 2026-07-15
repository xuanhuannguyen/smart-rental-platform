using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Entities.Media;

namespace SmartRentalPlatform.Domain.Entities.Billing;

public class MeterReading
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid ContractId { get; set; }
    public Guid ServiceTypeId { get; set; }
    public DateOnly BillingPeriodStart { get; set; }
    public DateOnly BillingPeriodEnd { get; set; }
    public decimal PreviousReading { get; set; }
    public decimal CurrentReading { get; set; }
    public decimal Consumption { get; set; }
    public Guid? ProofMediaAssetId { get; set; }
    public decimal? AiReading { get; set; }
    public string? AiRawText { get; set; }
    public bool WasManuallyCorrected { get; set; }
    public Guid RecordedByLandlordUserId { get; set; }
    public DateTimeOffset ReadingAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Room Room { get; set; } = null!;
    public RentalContract RentalContract { get; set; } = null!;
    public BillingServiceType ServiceType { get; set; } = null!;
    public User RecordedByLandlord { get; set; } = null!;
    public MediaAsset? ProofMediaAsset { get; set; }
    public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
}
