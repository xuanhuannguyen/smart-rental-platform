namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class StartESignEnvelopeResponse
{
    public Guid EnvelopeId { get; set; }
    public Guid ContractId { get; set; }
    public Guid? AppendixId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool RequiresOtp { get; set; } = true;
    public List<ESignParticipantResponse> Participants { get; set; } = new();
}
