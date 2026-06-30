using SmartRentalPlatform.Contracts.RoomingHouses.Requests;

namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public class ParsedRoomingHouseSearchCriteria
{
    public string? Keyword { get; set; }

    public string? PlaceText { get; set; }

    public string? ProvinceCode { get; set; }

    public string? WardCode { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public decimal? MinArea { get; set; }

    public decimal? MaxArea { get; set; }

    public int? MinOccupants { get; set; }

    public List<int> AmenityIds { get; set; } = new();

    public List<int> RoomAmenityIds { get; set; } = new();

    public List<Guid> RecentRoomingHouseIds { get; set; } = new();

    public List<int> PreferredAmenityIds { get; set; } = new();

    public List<int> PreferredRoomAmenityIds { get; set; } = new();

    public decimal? CenterLat { get; set; }

    public decimal? CenterLng { get; set; }

    public decimal? RadiusKm { get; set; }

    public string Sort { get; set; } = "relevance";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 12;

    public bool AiAssisted { get; set; }

    public string? InterpretedQuery { get; set; }

    public List<string> RelaxedFields { get; set; } = new();

    public static ParsedRoomingHouseSearchCriteria FromRequest(RoomingHouseSearchRequest request)
    {
        return new ParsedRoomingHouseSearchCriteria
        {
            Keyword = request.Q,
            ProvinceCode = NormalizeEmpty(request.ProvinceCode),
            WardCode = NormalizeEmpty(request.WardCode),
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            MinArea = request.MinArea,
            MaxArea = request.MaxArea,
            MinOccupants = request.MinOccupants,
            AmenityIds = request.AmenityIds.Distinct().ToList(),
            RoomAmenityIds = request.RoomAmenityIds.Distinct().ToList(),
            RecentRoomingHouseIds = request.RecentRoomingHouseIds.Distinct().ToList(),
            PreferredAmenityIds = request.PreferredAmenityIds.Distinct().ToList(),
            PreferredRoomAmenityIds = request.PreferredRoomAmenityIds.Distinct().ToList(),
            CenterLat = request.CenterLat,
            CenterLng = request.CenterLng,
            RadiusKm = request.RadiusKm,
            Sort = string.IsNullOrWhiteSpace(request.Sort) ? "relevance" : request.Sort.Trim(),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private static string? NormalizeEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
