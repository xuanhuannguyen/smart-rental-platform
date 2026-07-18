namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ContractFileResponse
{
    public Guid Id { get; set; }

    public Guid RentalContractId { get; set; }

    public Guid? RentalContractAppendixId { get; set; }

    public Guid? MediaAssetId { get; set; }

    public string Purpose { get; set; } = string.Empty;

    public string? ViewUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
