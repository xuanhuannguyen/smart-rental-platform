namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class RequestContractSignatureOtpResponse
{
    public Guid ContractId { get; set; }

    public string SignerRole { get; set; } = default!;

    public DateTimeOffset ExpiresAt { get; set; }

    public string MaskedEmail { get; set; } = default!;
}
