namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ContractDetailResponse
{
    public Guid Id { get; set; }

    public Guid RentalRequestId { get; set; }

    public Guid RoomDepositId { get; set; }

    public Guid RoomId { get; set; }

    public string RoomNumber { get; set; } = string.Empty;

    public Guid RoomingHouseId { get; set; }

    public string RoomingHouseName { get; set; } = string.Empty;

    public Guid MainTenantUserId { get; set; }

    public string MainTenantName { get; set; } = string.Empty;

    public string ContractNumber { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal MonthlyRent { get; set; }

    public decimal DepositAmount { get; set; }

    public int PaymentDay { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? RoomSnapshot { get; set; }

    public DateTimeOffset? SignatureDeadlineAt { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public string? StatusReason { get; set; }

    public List<ContractOccupantResponse> Occupants { get; set; } = [];

    public List<ContractSignatureResponse> Signatures { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
