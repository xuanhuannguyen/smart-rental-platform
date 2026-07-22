namespace SmartRentalPlatform.Contracts.Chat.Responses;

public sealed class ChatFileUploadResponse
{
    public Guid? MediaAssetId { get; set; }

    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
}
