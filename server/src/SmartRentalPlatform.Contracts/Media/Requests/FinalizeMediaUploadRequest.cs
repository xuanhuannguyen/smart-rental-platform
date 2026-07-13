namespace SmartRentalPlatform.Contracts.Media.Requests;

public sealed class FinalizeMediaUploadRequest
{
    public Guid MediaAssetId { get; set; }

    public string? FileHash { get; set; }
}
