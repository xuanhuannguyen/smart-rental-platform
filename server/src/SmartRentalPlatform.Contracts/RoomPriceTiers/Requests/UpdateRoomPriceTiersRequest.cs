namespace SmartRentalPlatform.Contracts.RoomPriceTiers.Requests;

    public class UpdateRoomPriceTiersRequest
    {
        public List<RoomPriceTierRequest> PriceTiers { get; set; } = new();
    }