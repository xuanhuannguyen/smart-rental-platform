namespace SmartRentalPlatform.Contracts.RoomPriceTiers
{
    public class UpdateRoomPriceTiersRequest
    {
        public List<RoomPriceTierRequest> PriceTiers { get; set; } = new();
    }
}
