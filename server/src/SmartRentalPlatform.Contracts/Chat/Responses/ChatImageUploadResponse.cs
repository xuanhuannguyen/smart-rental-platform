namespace SmartRentalPlatform.Contracts.Chat.Responses;

public sealed class ChatImageUploadResponse
{
    public Guid? MediaAssetId { get; set; }

    public string Url { get; set; } = string.Empty;
}
