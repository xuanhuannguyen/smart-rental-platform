namespace SmartRentalPlatform.Contracts.Chat.Responses;

public sealed class SendChatMessageResponse
{
    public ChatMessageResponse Message { get; set; } = new();
    public ConversationResponse Conversation { get; set; } = new();
    public List<Guid> RecipientUserIds { get; set; } = new();
}
