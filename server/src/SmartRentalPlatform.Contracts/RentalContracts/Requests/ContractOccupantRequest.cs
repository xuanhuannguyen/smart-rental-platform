namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class ContractOccupantRequest
{
    public string? ClientReferenceId { get; set; }

    public string? GuardianClientReferenceId { get; set; }

    public string? Email { get; set; }

    public string? FullName { get; set; }

    public string? PhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? RelationshipToMainTenant { get; set; }

    public DateOnly MoveInDate { get; set; }

    public DateOnly? MoveOutDate { get; set; }

    public ContractOccupantDocumentRequest? Document { get; set; }
}
