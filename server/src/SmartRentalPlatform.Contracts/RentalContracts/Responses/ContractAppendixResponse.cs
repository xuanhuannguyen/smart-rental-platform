namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ContractAppendixResponse
{
    public Guid Id { get; set; }

    public Guid RentalContractId { get; set; }

    public string AppendixNumber { get; set; } = string.Empty;

    public DateOnly EffectiveDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public string? StatusReason { get; set; }

    public List<ContractAppendixChangeResponse> Changes { get; set; } = [];

    public List<ContractSignatureResponse> Signatures { get; set; } = [];

    public List<ContractFileResponse> Files { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
