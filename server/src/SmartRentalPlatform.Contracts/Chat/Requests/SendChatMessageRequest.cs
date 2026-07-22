namespace SmartRentalPlatform.Contracts.Chat.Requests;

public sealed class SendChatMessageRequest
{
    public string MessageType { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileContentType { get; set; }
    public long? FileSize { get; set; }
    public string? ClientMessageId { get; set; }
}
