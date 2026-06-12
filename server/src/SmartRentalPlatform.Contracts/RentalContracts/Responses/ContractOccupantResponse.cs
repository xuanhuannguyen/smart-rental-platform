namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ContractOccupantResponse
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string? Email { get; set; }

    public Guid? GuardianOccupantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public string? RelationshipToMainTenant { get; set; }

    public DateOnly MoveInDate { get; set; }

    public DateOnly? MoveOutDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public ContractOccupantDocumentResponse? Document { get; set; }
}
