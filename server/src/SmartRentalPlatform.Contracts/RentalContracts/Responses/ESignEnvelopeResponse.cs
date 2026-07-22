namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ESignEnvelopeResponse
{
    public Guid EnvelopeId { get; set; }
    public Guid ContractId { get; set; }
    public Guid? AppendixId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? SignedFileId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public IReadOnlyList<ESignParticipantResponse> Participants { get; set; } = [];
}
