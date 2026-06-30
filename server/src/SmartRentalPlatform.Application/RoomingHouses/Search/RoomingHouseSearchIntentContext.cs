using SmartRentalPlatform.Contracts.RoomingHouses.Requests;

namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public sealed class RoomingHouseSearchIntentContext
{
    public RoomingHouseSearchIntentContext(
        RoomingHouseSearchRequest request,
        NormalizedQuery normalizedQuery,
        ParsedRoomingHouseSearchCriteria criteria)
    {
        Request = request;
        NormalizedQuery = normalizedQuery;
        Criteria = criteria;
    }

    public RoomingHouseSearchRequest Request { get; }

    public NormalizedQuery NormalizedQuery { get; }

    public ParsedRoomingHouseSearchCriteria Criteria { get; }
}
