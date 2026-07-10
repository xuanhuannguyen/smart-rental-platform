namespace SmartRentalPlatform.Contracts.Files.Responses;

public class FileUploadResponse
{
    public Guid? MediaAssetId { get; set; }

    public string ObjectKey { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}

