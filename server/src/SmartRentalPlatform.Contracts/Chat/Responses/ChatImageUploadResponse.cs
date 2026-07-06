namespace SmartRentalPlatform.Contracts.Chat.Responses;

public sealed class ChatImageUploadResponse
{
    public string ObjectKey { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
