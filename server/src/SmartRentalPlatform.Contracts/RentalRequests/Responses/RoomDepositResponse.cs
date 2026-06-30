namespace SmartRentalPlatform.Contracts.RentalRequests.Responses;

public class RoomDepositResponse
{
    public Guid Id { get; set; }

    public Guid RentalRequestId { get; set; }

    public Guid RoomId { get; set; }

    public Guid TenantUserId { get; set; }

    public Guid LandlordUserId { get; set; }

    public decimal DepositAmount { get; set; }

    public string Currency { get; set; } = "VND";

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? PaymentDeadlineAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public DateTimeOffset? RefundedAt { get; set; }

    public DateTimeOffset? ForfeitedAt { get; set; }

    public decimal? RefundAmount { get; set; }

    public decimal? ForfeitedAmount { get; set; }

    public string? Note { get; set; }

    public Guid? PaymentTransferGroupId { get; set; }

    public Guid? RefundTransferGroupId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
