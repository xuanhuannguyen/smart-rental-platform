namespace SmartRentalPlatform.Contracts.RentalContracts.Responses;

public class ESignParticipantResponse
{
    public Guid UserId { get; set; }
    public string SignerRole { get; set; } = string.Empty;
    public int SigningOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SigningUrl { get; set; }
}
