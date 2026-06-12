namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class TerminateContractRequest
{
    public ContractTerminationType TerminationType { get; set; }
    public decimal DamageFee { get; set; } = 0;
    public string Reason { get; set; } = string.Empty;
}
