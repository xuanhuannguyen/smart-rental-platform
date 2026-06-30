namespace SmartRentalPlatform.Contracts.RoomingHouses.Responses;

public class RoomingHouseSearchMetadataResponse
{
    public bool AiAssisted { get; set; }

    public string? OriginalQuery { get; set; }

    public string? InterpretedQuery { get; set; }

    public List<string> RelaxedFields { get; set; } = new();
}
