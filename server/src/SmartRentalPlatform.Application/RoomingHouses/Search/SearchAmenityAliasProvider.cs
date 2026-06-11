using System.Text.Json;

namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public sealed class SearchAmenityAliasProvider
{
    private const string AliasFileName = "search-amenity-aliases.json";

    public IReadOnlyList<AmenityAliasGroup> GetAliases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "RoomingHouses", "Search", AliasFileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, AliasFileName);
        }

        if (!File.Exists(path))
        {
            return GetFallbackAliases();
        }

        try
        {
            var json = File.ReadAllText(path);
            var groups = JsonSerializer.Deserialize<List<AmenityAliasGroup>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return groups?.Count > 0 ? groups : GetFallbackAliases();
        }
        catch
        {
            return GetFallbackAliases();
        }
    }

    private static IReadOnlyList<AmenityAliasGroup> GetFallbackAliases() =>
    [
        new(5, SearchAmenityScope.Room, ["máy lạnh", "may lanh", "điều hòa", "dieu hoa", "air con", "ac"]),
        new(6, SearchAmenityScope.Room, ["gác", "gac", "gác lửng", "gac lung", "gác xép", "gac xep"]),
        new(7, SearchAmenityScope.Room, ["wc riêng", "wc rieng", "nhà vệ sinh riêng", "nha ve sinh rieng", "toilet riêng", "toilet rieng"]),
        new(8, SearchAmenityScope.Room, ["ban công", "ban cong", "balcony"]),
        new(1, SearchAmenityScope.Both, ["wifi", "internet", "net", "mạng", "mang"]),
        new(4, SearchAmenityScope.Both, ["máy giặt", "may giat", "giặt sấy", "giat say"]),
        new(2, SearchAmenityScope.House, ["camera", "an ninh", "bảo vệ", "bao ve", "cctv"]),
        new(3, SearchAmenityScope.House, ["chỗ để xe", "cho de xe", "để xe", "de xe", "gửi xe", "giu xe", "bãi xe", "bai xe"])
    ];
}

public sealed record AmenityAliasGroup(int Id, SearchAmenityScope Scope, string[] Aliases);

public enum SearchAmenityScope
{
    House,
    Room,
    Both
}
