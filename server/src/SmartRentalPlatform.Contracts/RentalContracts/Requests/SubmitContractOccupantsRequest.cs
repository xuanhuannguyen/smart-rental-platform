namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class SubmitContractOccupantsRequest
{
    public List<ContractOccupantRequest> Occupants { get; set; } = [];
}
