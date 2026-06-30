namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ContractHistoryItemResponse
{
    public Guid Id { get; set; }

    public Guid RentalRequestId { get; set; }

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

    public int MaxOccupants { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? StatusReason { get; set; }

    public DateTimeOffset? SignatureDeadlineAt { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public string? TerminationType { get; set; }

    public bool IsAwaitingFinalInvoice { get; set; }

    public bool IsMainTenant { get; set; }

    public bool WasMainTenant { get; set; }

    public bool IsFormerMainTenant { get; set; }

    public bool IsCoTenant { get; set; }

    public bool IsFormerCoTenant { get; set; }

    public string CurrentUserRelation { get; set; } = string.Empty;

    public Guid? CurrentUserOccupantId { get; set; }

    public string? CurrentUserOccupantStatus { get; set; }

    public DateOnly? CurrentUserMoveInDate { get; set; }

    public DateOnly? CurrentUserMoveOutDate { get; set; }

    public Guid? SnapshotAtAppendixId { get; set; }

    public DateOnly? SnapshotAtDate { get; set; }

    public List<ContractOccupantResponse> Occupants { get; set; } = [];

    public bool CanViewRawContract { get; set; }

    public bool CanViewMaskedContract { get; set; }

    public bool CanCreateAppendix { get; set; }

    public bool CanTerminateContract { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
