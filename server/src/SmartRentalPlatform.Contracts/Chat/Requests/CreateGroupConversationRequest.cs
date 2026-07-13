namespace SmartRentalPlatform.Contracts.Chat.Requests;

public sealed class CreateGroupConversationRequest
{
    public string? Title { get; set; }
    public List<Guid> ParticipantUserIds { get; set; } = new();
    public Guid? RoomingHouseId { get; set; }
    public string? AvatarUrl { get; set; }
}
