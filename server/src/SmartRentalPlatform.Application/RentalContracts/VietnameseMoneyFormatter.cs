using System.Globalization;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class VietnameseMoneyFormatter
{
    private static readonly string[] Digits =
        ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];

    private static readonly string[] Scales = [string.Empty, "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ"];

    public static string Format(decimal value)
    {
        var amount = decimal.ToInt64(decimal.Truncate(value));
        if (amount == 0)
        {
            return "Không đồng";
        }

        var groups = new List<int>();
        var remaining = Math.Abs(amount);
        while (remaining > 0)
        {
            groups.Add((int)(remaining % 1000));
            remaining /= 1000;
        }

        var words = new List<string>();
        for (var index = groups.Count - 1; index >= 0; index--)
        {
            var group = groups[index];
            if (group == 0)
            {
                continue;
            }

            var includeHundreds = index < groups.Count - 1 && group < 100;
            words.Add(ReadGroup(group, includeHundreds));
            if (index < Scales.Length && !string.IsNullOrEmpty(Scales[index]))
            {
                words.Add(Scales[index]);
            }
        }

        var result = string.Join(' ', words.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (amount < 0)
        {
            result = $"âm {result}";
        }

        return $"{char.ToUpper(result[0], CultureInfo.GetCultureInfo("vi-VN"))}{result[1..]} đồng";
    }

    private static string ReadGroup(int value, bool includeHundreds)
    {
        var hundreds = value / 100;
        var tens = value % 100 / 10;
        var units = value % 10;
        var words = new List<string>();

        if (hundreds > 0 || includeHundreds)
        {
            words.Add(Digits[hundreds]);
            words.Add("trăm");
        }

        if (tens > 1)
        {
            words.Add(Digits[tens]);
            words.Add("mươi");
            if (units == 1)
            {
                words.Add("mốt");
            }
            else if (units == 4)
            {
                words.Add("tư");
            }
            else if (units == 5)
            {
                words.Add("lăm");
            }
            else if (units > 0)
            {
                words.Add(Digits[units]);
            }
        }
        else if (tens == 1)
        {
            words.Add("mười");
            words.Add(units == 5 ? "lăm" : units > 0 ? Digits[units] : string.Empty);
        }
        else if (units > 0)
        {
            if (hundreds > 0 || includeHundreds)
            {
                words.Add("lẻ");
            }

            words.Add(Digits[units]);
        }

        return string.Join(' ', words.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
