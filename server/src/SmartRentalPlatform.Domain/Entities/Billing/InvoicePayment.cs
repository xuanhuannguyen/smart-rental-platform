using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Domain.Entities.Billing;

public class InvoicePayment
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid TenantUserId { get; set; }
    public Guid LandlordUserId { get; set; }
    public decimal Amount { get; set; }
    public Guid WalletTransferGroupId { get; set; }
    public InvoicePaymentStatus Status { get; set; } = InvoicePaymentStatus.Succeeded;
    public DateTimeOffset PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Invoice Invoice { get; set; } = null!;
    public User Tenant { get; set; } = null!;
    public User Landlord { get; set; } = null!;
}
