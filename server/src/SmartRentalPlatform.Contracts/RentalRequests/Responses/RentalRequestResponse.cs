using SmartRentalPlatform.Contracts.RentalContracts.Responses;

namespace SmartRentalPlatform.Contracts.RentalRequests.Responses;

public class RentalRequestResponse
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public Guid RoomingHouseId { get; set; }

    public string RoomingHouseName { get; set; } = string.Empty;

    public Guid TenantUserId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public Guid? ApprovedByLandlordId { get; set; }

    public DateOnly DesiredStartDate { get; set; }

    public DateOnly ExpectedEndDate { get; set; }

    public int ExpectedOccupantCount { get; set; }

    public decimal MonthlyRentSnapshot { get; set; }

    public decimal DepositAmountSnapshot { get; set; }

    public string? TenantNote { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? RespondedAt { get; set; }

    public string? RejectedReason { get; set; }

    public RoomDepositResponse? Deposit { get; set; }

    public ContractBriefResponse? Contract { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
