namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ContractSignatureResponse
{
    public Guid Id { get; set; }

    public Guid SignerUserId { get; set; }

    public string SignerRole { get; set; } = string.Empty;

    public string SignatureMethod { get; set; } = string.Empty;

    public DateTimeOffset SignedAt { get; set; }
}
