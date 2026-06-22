namespace SmartRentalPlatform.Contracts.RoomingHouses.Requests;

public class RoomingHouseAiChatRequest
{
    public string Message { get; set; } = string.Empty;

    public string Context { get; set; } = "home";

    public Guid? RoomingHouseId { get; set; }

    public string Mode { get; set; } = "detailed";

    public string? ConversationId { get; set; }
}
