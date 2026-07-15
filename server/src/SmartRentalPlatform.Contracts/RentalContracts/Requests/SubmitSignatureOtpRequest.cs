namespace SmartRentalPlatform.Contracts.RentalContracts.Requests;

public class SubmitSignatureOtpRequest
{
    public string OtpCode { get; set; } = string.Empty;
    public string SignatureImageBase64 { get; set; } = string.Empty;
}
