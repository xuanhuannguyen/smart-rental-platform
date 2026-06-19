using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartRentalPlatform.Application.RoomingHouses.Search;

public sealed record NormalizedQuery(
    string Raw,
    string WithDiacritics,
    string WithoutDiacritics,
    IReadOnlyList<int> WithoutDiacriticsToWithDiacriticsMap)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(WithDiacritics);
}

public sealed partial class QueryNormalizer
{
    public NormalizedQuery Normalize(string? raw)
    {
        var original = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(original))
        {
            return new NormalizedQuery(string.Empty, string.Empty, string.Empty, []);
        }

        var lower = original
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();
        var withDiacritics = CollapseWhitespaceAndPunctuation(lower);
        var (withoutDiacritics, map) = BuildUnaccentWithIndexMap(withDiacritics);

        return new NormalizedQuery(original, withDiacritics, withoutDiacritics, map);
    }

    private static string CollapseWhitespaceAndPunctuation(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsWhiteSpace(current))
            {
                AppendSpace(builder, ref previousWasSpace);
                continue;
            }

            if (IsStripPunctuation(value, index))
            {
                AppendSpace(builder, ref previousWasSpace);
                continue;
            }

            builder.Append(current);
            previousWasSpace = false;
        }

        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    private static bool IsStripPunctuation(string value, int index)
    {
        var current = value[index];
        if (current is not ('.' or ',' or '!' or '?' or ';' or ':' or '"' or '\'' or '(' or ')' or '[' or ']'))
        {
            return false;
        }

        var betweenDigits = index > 0 &&
                            index < value.Length - 1 &&
                            char.IsDigit(value[index - 1]) &&
                            char.IsDigit(value[index + 1]);
        return !betweenDigits;
    }

    private static void AppendSpace(StringBuilder builder, ref bool previousWasSpace)
    {
        if (previousWasSpace || builder.Length == 0)
        {
            return;
        }

        builder.Append(' ');
        previousWasSpace = true;
    }

    private static (string Text, IReadOnlyList<int> Map) BuildUnaccentWithIndexMap(string value)
    {
        var builder = new StringBuilder(value.Length);
        var map = new List<int>(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            var normalized = current.ToString().Normalize(NormalizationForm.FormD);
            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                builder.Append(ch == 'đ' ? 'd' : ch);
                map.Add(index);
            }
        }

        return (builder.ToString().Normalize(NormalizationForm.FormC), map);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
