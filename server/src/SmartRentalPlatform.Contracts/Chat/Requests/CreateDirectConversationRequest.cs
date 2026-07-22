namespace SmartRentalPlatform.Contracts.Chat.Requests;

public sealed class CreateDirectConversationRequest
{
    public Guid OtherUserId { get; set; }
}
