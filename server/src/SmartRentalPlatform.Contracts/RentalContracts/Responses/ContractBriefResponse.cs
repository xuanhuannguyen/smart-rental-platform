namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ContractBriefResponse
{
    public Guid Id { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? SignatureDeadlineAt { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public string? StatusReason { get; set; }
}
