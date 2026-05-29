namespace SmartRentalPlatform.Contracts.RoomingHouses.Responses;

public class RoomingHouseOnboardingResponse
{
    public string Status { get; set; } = string.Empty;

    public bool HasRoomingHouse { get; set; }

    public bool CanCreateDraft { get; set; }

    public bool CanEdit { get; set; }

    public bool CanSubmit { get; set; }

    public bool CanEnterLandlordDashboard { get; set; }

    public Guid? RoomingHouseId { get; set; }

    public RoomingHouseDetailResponse? RoomingHouse { get; set; }
}
