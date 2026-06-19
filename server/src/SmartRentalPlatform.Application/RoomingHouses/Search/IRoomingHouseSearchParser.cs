using SmartRentalPlatform.Contracts.RoomingHouses.Requests;

namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public interface IRoomingHouseSearchParser
{
    ParsedRoomingHouseSearchCriteria Parse(RoomingHouseSearchRequest request);
}
