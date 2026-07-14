namespace SmartRentalPlatform.Contracts.Files.Responses;

public class FileUploadResponse
{
    public Guid? MediaAssetId { get; set; }

    public string Url { get; set; } = string.Empty;
}
