using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Domain.Entities.Leasing;

public class Contract
{
    public Guid Id { get; set; }
    public Guid? RentalRequestId { get; set; }
    public Guid? RoomDepositId { get; set; }
    public Guid RoomId { get; set; }
    public Guid MainTenantUserId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public int PaymentDay { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public string? RoomSnapshot { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Room Room { get; set; } = null!;
    public User MainTenant { get; set; } = null!;
    public ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
