using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public sealed class RoomingHouseSearchCandidate
{
    public RoomingHouseSearchCandidate(
        RoomingHouseSearchCandidateData candidateData,
        ParsedRoomingHouseSearchCriteria criteria)
    {
        CandidateData = candidateData;
        Criteria = criteria;
    }

    public RoomingHouseSearchCandidateData CandidateData { get; }

    public ParsedRoomingHouseSearchCriteria Criteria { get; }
}
