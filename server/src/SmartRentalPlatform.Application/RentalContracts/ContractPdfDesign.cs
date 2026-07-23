using QuestPDF.Drawing;
using QuestPDF.Helpers;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class ContractPdfDesign
{
    private const string PreferredFontFamily = "Times New Roman";
    private const string FallbackFontFamily = "Liberation Serif";

    public static string FontFamily { get; private set; } = PreferredFontFamily;
    public const float PageMarginHorizontal = 56.7f;
    // Header/footer are laid out inside the page margin area by QuestPDF.
    // Combined with their fixed heights, these values produce the 21 mm / 19 mm visual margins of the approved sample.
    public const float PageMarginTop = 32f;
    public const float PageMarginBottom = 32f;
    public const float BodyFontSize = 10.4f;
    public const float BodyLineHeight = 1.4f;
    public const string PrimaryColor = "#0F3D66";
    public const string HeaderBackground = "#E8F1F8";
    public const string BorderColor = "#94A3B8";
    public const string LightBorderColor = "#CBD5E1";
    public const string MutedTextColor = "#475569";
    public const string PreviewColor = "#B91C1C";

    private static readonly object FontRegistrationLock = new();
    private static bool fontsRegistered;

    public static void EnsureFontsRegistered()
    {
        if (fontsRegistered)
        {
            return;
        }

        lock (FontRegistrationLock)
        {
            if (fontsRegistered)
            {
                return;
            }

            var candidates = GetFontCandidates()
                .Where(candidate => File.Exists(candidate.Path))
                .GroupBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            var registeredCount = 0;
            foreach (var candidate in candidates)
            {
                using var stream = File.OpenRead(candidate.Path);
                FontManager.RegisterFont(stream);
                registeredCount++;
            }

            if (registeredCount == 0)
            {
                throw new InvalidOperationException(
                    "No supported contract PDF font files were found. Install Times New Roman or Liberation Serif fonts before rendering contracts.");
            }

            FontFamily = candidates.Any(candidate => candidate.Family == PreferredFontFamily)
                ? PreferredFontFamily
                : FallbackFontFamily;

            fontsRegistered = true;
        }
    }

    private static IEnumerable<(string Path, string Family)> GetFontCandidates()
    {
        var windowsFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrWhiteSpace(windowsFonts))
        {
            yield return (Path.Combine(windowsFonts, "times.ttf"), PreferredFontFamily);
            yield return (Path.Combine(windowsFonts, "timesbd.ttf"), PreferredFontFamily);
            yield return (Path.Combine(windowsFonts, "timesi.ttf"), PreferredFontFamily);
            yield return (Path.Combine(windowsFonts, "timesbi.ttf"), PreferredFontFamily);
        }

        var linuxRoots = new[]
        {
            "/usr/share/fonts/truetype/msttcorefonts",
            "/usr/share/fonts/truetype/microsoft",
            "/usr/local/share/fonts"
        };

        var linuxNames = new[]
        {
            "Times_New_Roman.ttf",
            "Times_New_Roman_Bold.ttf",
            "Times_New_Roman_Italic.ttf",
            "Times_New_Roman_Bold_Italic.ttf",
            "times.ttf",
            "timesbd.ttf",
            "timesi.ttf",
            "timesbi.ttf"
        };

        foreach (var root in linuxRoots)
        {
            foreach (var name in linuxNames)
            {
                yield return (Path.Combine(root, name), PreferredFontFamily);
            }
        }

        var liberationRoots = new[]
        {
            "/usr/share/fonts/truetype/liberation2",
            "/usr/share/fonts/truetype/liberation",
            "/usr/local/share/fonts"
        };

        var liberationNames = new[]
        {
            "LiberationSerif-Regular.ttf",
            "LiberationSerif-Bold.ttf",
            "LiberationSerif-Italic.ttf",
            "LiberationSerif-BoldItalic.ttf"
        };

        foreach (var root in liberationRoots)
        {
            foreach (var name in liberationNames)
            {
                yield return (Path.Combine(root, name), FallbackFontFamily);
            }
        }
    }
}
