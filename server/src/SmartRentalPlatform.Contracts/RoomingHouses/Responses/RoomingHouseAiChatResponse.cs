namespace SmartRentalPlatform.Contracts.RoomingHouses.Responses;

public class RoomingHouseAiChatResponse
{
    public string Reply { get; set; } = string.Empty;

    public string Intent { get; set; } = "general";

    public decimal Confidence { get; set; }

    public bool AiAssisted { get; set; }

    /// <summary>
    /// Persistent conversation identifier for multi-turn chat.
    /// Client must send this back in subsequent requests to maintain context.
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    public List<RoomingHouseSearchItemResponse> RoomingHouses { get; set; } = new();

    public List<NearbyPlaceResponse> NearbyPlaces { get; set; } = new();

    public List<string> FollowUpQuestions { get; set; } = new();

    public List<string> MissingInformation { get; set; } = new();

    public List<string> UsedSources { get; set; } = new();
}
