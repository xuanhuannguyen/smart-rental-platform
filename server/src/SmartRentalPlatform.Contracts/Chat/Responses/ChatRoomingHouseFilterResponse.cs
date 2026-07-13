namespace SmartRentalPlatform.Contracts.Chat.Responses;

public sealed class ChatRoomingHouseFilterResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}
