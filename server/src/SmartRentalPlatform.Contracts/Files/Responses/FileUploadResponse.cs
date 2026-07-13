namespace SmartRentalPlatform.Contracts.Files.Responses;

public class FileUploadResponse
{
    public Guid? MediaAssetId { get; set; }

    /// <summary>
    /// Compatibility-only storage key for legacy callers. New callers should use MediaAssetId plus media routes.
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public bool IsCompatibilityResponse { get; set; }

    public string? CompatibilityWarning { get; set; }
}
