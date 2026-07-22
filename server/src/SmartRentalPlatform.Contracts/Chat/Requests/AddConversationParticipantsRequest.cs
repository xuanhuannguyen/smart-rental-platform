namespace SmartRentalPlatform.Contracts.Chat.Requests;

public sealed class AddConversationParticipantsRequest
{
    public List<Guid> UserIds { get; set; } = new();
}
