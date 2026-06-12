namespace SmartRentalPlatform.Contracts.RentalPolicies.Responses;

public class RentalPolicyResponse
{
    public Guid Id { get; set; }

    public Guid RoomingHouseId { get; set; }

    public int MinRentalMonths { get; set; }

    public int MaxRentalMonths { get; set; }

    public bool AllowShortTermRenewal { get; set; }

    public int RenewalNoticeDays { get; set; }

    public decimal DepositMonths { get; set; }

    public int DefaultPaymentDay { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

