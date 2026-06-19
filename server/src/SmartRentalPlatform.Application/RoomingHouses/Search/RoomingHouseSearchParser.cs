using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;

namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public partial class RoomingHouseSearchParser : IRoomingHouseSearchParser
{
    private readonly QueryNormalizer queryNormalizer = new();
    private readonly IReadOnlyList<AmenityAliasGroup> amenityAliases;

    public RoomingHouseSearchParser()
    {
        amenityAliases = new SearchAmenityAliasProvider().GetAliases();
    }

    public ParsedRoomingHouseSearchCriteria Parse(RoomingHouseSearchRequest request)
    {
        var criteria = ParsedRoomingHouseSearchCriteria.FromRequest(request);
        var query = queryNormalizer.Normalize(request.Q);
        if (query.IsEmpty)
        {
            return criteria;
        }

        var normalized = query.WithoutDiacritics;
        criteria.ProvinceCode ??= ParseProvinceCode(normalized);
        ParsePrice(normalized, criteria);
        ParseArea(normalized, criteria);
        ParseOccupants(normalized, criteria);
        ParseAmenities(normalized, criteria);
        ParseRadius(normalized, criteria);
        var knownPlace = ParseKnownPlace(normalized);
        if (knownPlace is not null)
        {
            criteria.PlaceText ??= knownPlace.PlaceText;
            criteria.ProvinceCode ??= knownPlace.ProvinceCode;
        }
        criteria.PlaceText ??= ParsePlaceText(query.WithDiacritics);
        criteria.PlaceText ??= ParsePlaceText(query.WithoutDiacritics);
        criteria.PlaceText ??= ParseFallbackPlaceText(query.WithoutDiacritics);
        criteria.Keyword = BuildKeyword(query);

        return criteria;
    }

    public static string Normalize(string value)
        => new QueryNormalizer().Normalize(value).WithoutDiacritics;

    private static string? ParseProvinceCode(string normalized)
    {
        if (ContainsAny(normalized, "da nang", "tp da nang", "thanh pho da nang", "dn"))
        {
            return "48";
        }

        if (ContainsAny(normalized, "hue", "tp hue", "thanh pho hue", "thua thien hue"))
        {
            return "46";
        }

        if (ContainsAny(normalized, "sai gon", "tphcm", "tp hcm", "tp ho chi minh", "hcm", "ho chi minh"))
        {
            return "79";
        }

        if (ContainsAny(normalized, "ha noi", "tp ha noi", "thanh pho ha noi", "hn"))
        {
            return "01";
        }

        return null;
    }

    private static void ParsePrice(string normalized, ParsedRoomingHouseSearchCriteria criteria)
    {
        var compactMoneyRangeMatch = CompactMoneyRangeRegex().Match(normalized);
        if (compactMoneyRangeMatch.Success)
        {
            criteria.MinPrice ??= ParseCompactMoney(
                compactMoneyRangeMatch.Groups["minMajor"].Value,
                compactMoneyRangeMatch.Groups["minMinor"].Value);
            criteria.MaxPrice ??= ParseCompactMoney(
                compactMoneyRangeMatch.Groups["maxMajor"].Value,
                compactMoneyRangeMatch.Groups["maxMinor"].Value);
            return;
        }

        var rangeMatch = PriceRangeRegex().Match(normalized);
        if (rangeMatch.Success)
        {
            criteria.MinPrice ??= ParseMoney(rangeMatch.Groups["min"].Value, rangeMatch.Groups["minUnit"].Value);
            criteria.MaxPrice ??= ParseMoney(rangeMatch.Groups["max"].Value, rangeMatch.Groups["maxUnit"].Value);
            return;
        }

        var compactRangeMatch = CompactPriceRangeRegex().Match(normalized);
        if (compactRangeMatch.Success)
        {
            criteria.MinPrice ??= ParseMoney(
                compactRangeMatch.Groups["min"].Value,
                compactRangeMatch.Groups["minUnit"].Value);
            criteria.MaxPrice ??= ParseMoney(
                compactRangeMatch.Groups["max"].Value,
                compactRangeMatch.Groups["maxUnit"].Value);
            return;
        }

        var maxMatch = MaxPriceRegex().Match(normalized);
        if (maxMatch.Success)
        {
            criteria.MaxPrice ??= maxMatch.Groups["minor"].Success
                ? ParseCompactMoney(maxMatch.Groups["value"].Value, maxMatch.Groups["minor"].Value)
                : ParseMoney(maxMatch.Groups["value"].Value, maxMatch.Groups["unit"].Value);
        }

        var minMatch = MinPriceRegex().Match(normalized);
        if (minMatch.Success)
        {
            criteria.MinPrice ??= minMatch.Groups["minor"].Success
                ? ParseCompactMoney(minMatch.Groups["value"].Value, minMatch.Groups["minor"].Value)
                : ParseMoney(minMatch.Groups["value"].Value, minMatch.Groups["unit"].Value);
        }

        if (criteria.MinPrice is null && criteria.MaxPrice is null)
        {
            var singleBudgetMatch = SingleBudgetRegex().Match(normalized);
            if (singleBudgetMatch.Success)
            {
                criteria.MaxPrice = singleBudgetMatch.Groups["minor"].Success
                    ? ParseCompactMoney(singleBudgetMatch.Groups["value"].Value, singleBudgetMatch.Groups["minor"].Value)
                    : ParseMoney(singleBudgetMatch.Groups["value"].Value, singleBudgetMatch.Groups["unit"].Value);
            }
        }
    }

    private static void ParseArea(string normalized, ParsedRoomingHouseSearchCriteria criteria)
    {
        var rangeMatch = AreaRangeRegex().Match(normalized);
        if (rangeMatch.Success)
        {
            criteria.MinArea ??= ParseDecimal(rangeMatch.Groups["min"].Value);
            criteria.MaxArea ??= ParseDecimal(rangeMatch.Groups["max"].Value);
            return;
        }

        var minMatch = MinAreaRegex().Match(normalized);
        if (minMatch.Success)
        {
            criteria.MinArea ??= ParseDecimal(minMatch.Groups["value"].Value);
        }

        var maxMatch = MaxAreaRegex().Match(normalized);
        if (maxMatch.Success)
        {
            criteria.MaxArea ??= ParseDecimal(maxMatch.Groups["value"].Value);
        }
    }

    private static void ParseOccupants(string normalized, ParsedRoomingHouseSearchCriteria criteria)
    {
        var match = OccupantsRegex().Match(normalized);
        if (match.Success && int.TryParse(match.Groups["value"].Value, out var occupants))
        {
            criteria.MinOccupants ??= occupants;
        }
    }

    private void ParseAmenities(string normalized, ParsedRoomingHouseSearchCriteria criteria)
    {
        foreach (var aliasGroup in amenityAliases)
        {
            if (!aliasGroup.Aliases.Any(alias => ContainsPhrase(normalized, alias)))
            {
                continue;
            }

            if (aliasGroup.Scope is SearchAmenityScope.House or SearchAmenityScope.Both)
            {
                AddDistinct(criteria.AmenityIds, aliasGroup.Id);
            }

            if (aliasGroup.Scope is SearchAmenityScope.Room or SearchAmenityScope.Both)
            {
                AddDistinct(criteria.RoomAmenityIds, aliasGroup.Id);
            }
        }
    }

    private static void ParseRadius(string normalized, ParsedRoomingHouseSearchCriteria criteria)
    {
        var match = RadiusRegex().Match(normalized);
        if (match.Success)
        {
            criteria.RadiusKm ??= ParseDecimal(match.Groups["value"].Value);
        }
    }

    private static string? ParsePlaceText(string rawQuery)
    {
        var match = PlaceTextRegex().Match(rawQuery);
        if (!match.Success)
        {
            return null;
        }

        var place = match.Groups["place"].Value.Trim(' ', ',', '.', ';', ':');
        return string.IsNullOrWhiteSpace(place) ? null : place;
    }

    private static KnownPlaceAlias? ParseKnownPlace(string normalized)
    {
        if (ContainsAny(normalized, "dai hoc fpt", "dh fpt", "fpt university", "truong fpt"))
        {
            return new KnownPlaceAlias("Đại học FPT Đà Nẵng", "48");
        }

        return null;
    }

    private static string? ParseFallbackPlaceText(string rawQuery)
    {
        if (!ContainsAny(Normalize(rawQuery), "phuong", "xa", "quan", "huyen", "duong", "hem", "kiet", "truong", "dai hoc", "cho"))
        {
            return null;
        }

        var cleaned = StripStructuredTerms(rawQuery);
        cleaned = GenericSearchTermRegex().Replace(cleaned, " ");
        cleaned = Normalize(cleaned);
        foreach (var noise in KeywordNoiseTokens)
        {
            cleaned = Regex.Replace(cleaned, $@"(^|\s){Regex.Escape(noise)}(\s|$)", " ");
        }

        cleaned = WhitespaceRegex().Replace(cleaned, " ").Trim();
        return cleaned.Length < 3 ? null : cleaned;
    }

    private static string BuildKeyword(NormalizedQuery query)
    {
        var keyword = StripStructuredTerms(query.WithoutDiacritics);
        keyword = Normalize(keyword);

        foreach (var noise in KeywordNoiseTokens)
        {
            keyword = Regex.Replace(keyword, $@"(^|\s){Regex.Escape(noise)}(\s|$)", " ");
        }

        return WhitespaceRegex().Replace(keyword, " ").Trim();
    }

    private static bool ContainsAny(string normalized, params string[] values)
    {
        return values.Any(value => ContainsPhrase(normalized, value));
    }

    private static bool ContainsPhrase(string normalized, string value)
    {
        return Regex.IsMatch(normalized, $@"(^|\s){Regex.Escape(Normalize(value))}(\s|$)");
    }

    private static void AddDistinct(List<int> target, int id)
    {
        if (!target.Contains(id))
        {
            target.Add(id);
        }
    }

    private static decimal ParseMoney(string value, string unit)
    {
        var amount = ParseDecimal(value);
        unit = unit.Trim();
        if (unit is "trieu" or "tr" or "m")
        {
            return amount * 1_000_000m;
        }

        if (unit is "k" or "nghin" or "ngan")
        {
            return amount * 1_000m;
        }

        if (unit is "vnd" or "dong" or "d")
        {
            return amount;
        }

        return amount >= 100_000m ? amount : amount * 1_000_000m;
    }

    private static decimal ParseCompactMoney(string major, string minor)
    {
        var majorAmount = ParseDecimal(major);
        var minorAmount = string.IsNullOrWhiteSpace(minor)
            ? 0m
            : ParseDecimal(minor) / (decimal)Math.Pow(10, minor.Length);

        return (majorAmount + minorAmount) * 1_000_000m;
    }

    private static decimal ParseDecimal(string value)
    {
        return decimal.TryParse(
            value.Replace(',', '.'),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var result)
            ? result
            : 0m;
    }

    private static string StripStructuredTerms(string value)
    {
        var keyword = PlaceTextRegex().Replace(value, " ");
        keyword = RadiusTextRegex().Replace(keyword, " ");
        keyword = PriceTextRegex().Replace(keyword, " ");
        keyword = CompactMoneyTextRegex().Replace(keyword, " ");
        keyword = AreaTextRegex().Replace(keyword, " ");
        keyword = OccupantTextRegex().Replace(keyword, " ");
        return keyword;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?:gia\s*)?(?:tu|min|toi thieu|it nhat)\s+(?<min>\d+(?:[\.,]\d+)?)\s*(?<minUnit>trieu|tr|m|k|nghin|ngan|vnd|dong|d)?\s*(?:den|toi|-)\s*(?<max>\d+(?:[\.,]\d+)?)\s*(?<maxUnit>trieu|tr|m|k|nghin|ngan|vnd|dong|d)?")]
    private static partial Regex PriceRangeRegex();

    [GeneratedRegex(@"(?<minMajor>\d+)tr(?<minMinor>\d+)?\s*(?:-|den|toi)\s*(?<maxMajor>\d+)tr(?<maxMinor>\d+)?")]
    private static partial Regex CompactMoneyRangeRegex();

    [GeneratedRegex(@"(?<min>\d+(?:[\.,]\d+)?)\s*(?<minUnit>trieu|tr|m|k|nghin|ngan|vnd|dong|d)?\s*(?:-|den|toi)\s*(?<max>\d+(?:[\.,]\d+)?)\s*(?<maxUnit>trieu|tr|m|k|nghin|ngan|vnd|dong|d)")]
    private static partial Regex CompactPriceRangeRegex();

    [GeneratedRegex(@"(?:gia\s*)?(?:duoi|toi da|max|maximum|khong qua|khong vuot qua|nho hon|re hon|<=|<)\s*(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>trieu|tr|m|k|nghin|ngan|vnd|dong|d)?|(?:gia\s*)?(?:duoi|toi da|max|maximum|khong qua|khong vuot qua|nho hon|re hon|<=|<)\s*(?<value>\d+)tr(?<minor>\d+)|(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>trieu|tr|m|k|nghin|ngan|vnd|dong|d)\s*(?:tro xuong|do lai|la duoc)|(?<value>\d+)tr(?<minor>\d+)\s*(?:tro xuong|do lai|la duoc)")]
    private static partial Regex MaxPriceRegex();

    [GeneratedRegex(@"(?:gia\s*)?(?:tren|tu|min|minimum|toi thieu|it nhat|lon hon|>=|>)\s*(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>trieu|tr|m|k|nghin|ngan|vnd|dong|d)|(?:gia\s*)?(?:tren|tu|min|minimum|toi thieu|it nhat|lon hon|>=|>)\s*(?<value>\d+)tr(?<minor>\d+)|(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>trieu|tr|m|k|nghin|ngan|vnd|dong|d)\s*(?:tro len)|(?<value>\d+)tr(?<minor>\d+)\s*(?:tro len)")]
    private static partial Regex MinPriceRegex();

    [GeneratedRegex(@"(?<value>\d+)tr(?<minor>\d+)|(?<value>\d+(?:[\.,]\d+)?)\s*(?<unit>trieu|tr|m|k|nghin|ngan|vnd|dong|d)")]
    private static partial Regex SingleBudgetRegex();

    [GeneratedRegex(@"tu\s+(?<min>\d+(?:[\.,]\d+)?)\s*m(?:2|²)?\s*(?:den|toi|-)\s*(?<max>\d+(?:[\.,]\d+)?)\s*m(?:2|²)?")]
    private static partial Regex AreaRangeRegex();

    [GeneratedRegex(@"(?:tren|tu|>=|>)\s*(?<value>\d+(?:[\.,]\d+)?)\s*m(?:2|²)?")]
    private static partial Regex MinAreaRegex();

    [GeneratedRegex(@"(?:duoi|toi da|<=|<)\s*(?<value>\d+(?:[\.,]\d+)?)\s*m(?:2|²)?")]
    private static partial Regex MaxAreaRegex();

    [GeneratedRegex(@"(?<value>\d+)\s*(?:nguoi|ng)")]
    private static partial Regex OccupantsRegex();

    [GeneratedRegex(@"(?:ban kinh|trong vong|pham vi|cach|duoi|khong qua)\s*(?<value>\d+(?:[\.,]\d+)?)\s*km|(?<value>\d+(?:[\.,]\d+)?)\s*km\s*(?:quanh day|do lai|tro lai)")]
    private static partial Regex RadiusRegex();

    [GeneratedRegex(@"(?:gần|quanh|xung quanh|khu vực|khu vuc|gan)\s+(?<place>.*?)(?=\s+(?:bán kính|ban kinh|trong vòng|trong vong|phạm vi|pham vi|cách|cach|dưới|duoi|từ|tu|giá|gia|có|co|\d+\s*km)|$)", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceTextRegex();

    [GeneratedRegex(@"(?:bán kính|ban kinh|trong vòng|trong vong|dưới|duoi|không quá|khong qua)\s*\d+(?:[\.,]\d+)?\s*km", RegexOptions.IgnoreCase)]
    private static partial Regex RadiusTextRegex();

    [GeneratedRegex(@"(?:giá|gia)?\s*(?:dưới|duoi|tối đa|toi da|max|không quá|khong qua|trên|tren|từ|tu|min|tối thiểu|toi thieu|ít nhất|it nhat)\s*\d+(?:[\.,]\d+)?\s*(?:triệu|trieu|tr|m|k|nghìn|nghin|ngàn|ngan|vnd|đồng|dong|đ|d)?(?:\s*(?:đến|den|tới|toi|-)\s*\d+(?:[\.,]\d+)?\s*(?:triệu|trieu|tr|m|k|nghìn|nghin|ngàn|ngan|vnd|đồng|dong|đ|d)?)?|\d+(?:[\.,]\d+)?\s*(?:triệu|trieu|tr|m|k|nghìn|nghin|ngàn|ngan|vnd|đồng|dong|đ|d)(?:\s*(?:đến|den|tới|toi|-)\s*\d+(?:[\.,]\d+)?\s*(?:triệu|trieu|tr|m|k|nghìn|nghin|ngàn|ngan|vnd|đồng|dong|đ|d)?)?(?:\s*(?:trở xuống|tro xuong|trở lên|tro len))?", RegexOptions.IgnoreCase)]
    private static partial Regex PriceTextRegex();

    [GeneratedRegex(@"\d+tr\d*(?:\s*(?:-|đến|den|tới|toi)\s*\d+tr\d*)?", RegexOptions.IgnoreCase)]
    private static partial Regex CompactMoneyTextRegex();

    [GeneratedRegex(@"(?:dưới|duoi|tối đa|toi da|trên|tren|từ|tu)?\s*\d+(?:[\.,]\d+)?\s*m(?:2|²)", RegexOptions.IgnoreCase)]
    private static partial Regex AreaTextRegex();

    [GeneratedRegex(@"\d+\s*(?:người|nguoi|ng)", RegexOptions.IgnoreCase)]
    private static partial Regex OccupantTextRegex();

    [GeneratedRegex(@"\b(?:phòng|phong|khu trọ|khu tro|nhà trọ|nha tro|cho thuê|cho thue|cần tìm|can tim|tìm|tim|ở|o|có|co)\b", RegexOptions.IgnoreCase)]
    private static partial Regex GenericSearchTermRegex();

    private static readonly string[] KeywordNoiseTokens =
    [
        "da nang",
        "dn",
        "sai gon",
        "tphcm",
        "tp hcm",
        "hcm",
        "ho chi minh",
        "ha noi",
        "hn",
        "phong",
        "khu",
        "nha",
        "tro",
        "khu tro",
        "nha tro",
        "cho thue",
        "thue",
        "can tim",
        "tim",
        "o",
        "co",
        "wifi",
        "internet",
        "mang",
        "cap quang",
        "may lanh",
        "dieu hoa",
        "air conditioner",
        "ac",
        "lam mat",
        "gac",
        "gac lung",
        "gac xep",
        "tang lung",
        "wc rieng",
        "ve sinh rieng",
        "nha ve sinh rieng",
        "toilet rieng",
        "phong tam rieng",
        "khep kin",
        "ban cong",
        "logia",
        "cua so lon",
        "may giat",
        "giat say",
        "khu giat",
        "phong giat",
        "camera",
        "an ninh",
        "bao ve",
        "bao ve 24/7",
        "khoa van tay",
        "cho de xe",
        "giu xe",
        "bai xe",
        "de xe",
        "parking",
        "xe may",
        "ham xe",
        "gara"
    ];

    private sealed record KnownPlaceAlias(string PlaceText, string? ProvinceCode);
}
