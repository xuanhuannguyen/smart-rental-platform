namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ContractFileResponse
{
    public Guid Id { get; set; }

    public Guid RentalContractId { get; set; }

    public Guid? RentalContractAppendixId { get; set; }

    public string StorageObjectKey { get; set; } = string.Empty;

    public string FileVariant { get; set; } = string.Empty;

    public string? FileUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
