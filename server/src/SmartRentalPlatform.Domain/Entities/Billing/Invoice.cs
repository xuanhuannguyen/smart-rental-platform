using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Domain.Entities.Billing;

public class Invoice
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid RoomId { get; set; }
    public Guid TenantUserId { get; set; }
    public Guid LandlordUserId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateOnly BillingPeriodStart { get; set; }
    public DateOnly BillingPeriodEnd { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal RentAmount { get; set; }
    public decimal UtilityAmount { get; set; }
    public decimal ServiceAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public string? Note { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public RentalContract RentalContract { get; set; } = null!;
    public Room Room { get; set; } = null!;
    public User Tenant { get; set; } = null!;
    public User Landlord { get; set; } = null!;
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public Guid? WalletTransferGroupId { get; set; }
}
