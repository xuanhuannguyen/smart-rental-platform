using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Models.ESign;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class SignatureImageNormalizer
{
    private const int MaximumByteLength = 2_000_000;
    private const int MinimumDimension = 16;
    private const int MaximumDimension = 4096;
    private const int TargetCanvasWidth = 720;
    private const int TargetCanvasHeight = 240;
    private const int TargetPadding = 24;
    private const byte AlphaThreshold = 20;
    private const byte WhiteThreshold = 245;

    public static ESignSignatureImage Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw InvalidImage("Vui long cung cap anh chu ky hop le.");
        }

        var normalized = value.Trim();
        string? declaredMediaType = null;
        if (normalized.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = normalized.IndexOf(',');
            if (commaIndex <= 5)
            {
                throw InvalidImage("Data URL cua anh chu ky khong hop le.");
            }

            declaredMediaType = ParseMediaType(normalized[..commaIndex]);
            if (!IsSupportedMediaType(declaredMediaType))
            {
                throw InvalidImage("Dinh dang anh chu ky khong duoc ho tro hoac bi loi.");
            }

            normalized = normalized[(commaIndex + 1)..];
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(normalized);
        }
        catch (FormatException)
        {
            throw InvalidImage("Anh chu ky khong phai du lieu base64 hop le.");
        }

        if (bytes.Length is < 32 or > MaximumByteLength)
        {
            throw InvalidImage("Anh chu ky phai co dung luong tu 32 byte den 2 MB.");
        }

        byte[] normalizedBytes;
        try
        {
            using var image = Image.Load<Rgba32>(bytes, out var format);
            var actualMediaType = ResolveMediaType(format);
            if (declaredMediaType is not null &&
                !string.Equals(NormalizeJpegMediaType(declaredMediaType), actualMediaType, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidImage("Data URL cua anh chu ky khong khop voi dinh dang tep.");
            }

            if (image.Width is < MinimumDimension or > MaximumDimension ||
                image.Height is < MinimumDimension or > MaximumDimension)
            {
                throw InvalidImage(
                    $"Kich thuoc anh chu ky phai tu {MinimumDimension}x{MinimumDimension} den {MaximumDimension}x{MaximumDimension} pixel.");
            }

            var signatureBounds = FindSignatureBounds(image);
            using var cropped = image.Clone(x => x.Crop(signatureBounds));
            using var fitted = cropped.Clone(x => x.Resize(new ResizeOptions
            {
                Size = new Size(TargetCanvasWidth - TargetPadding * 2, TargetCanvasHeight - TargetPadding * 2),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));

            using var canvas = new Image<Rgba32>(TargetCanvasWidth, TargetCanvasHeight, Color.White);
            var offsetX = (TargetCanvasWidth - fitted.Width) / 2;
            var offsetY = (TargetCanvasHeight - fitted.Height) / 2;
            canvas.Mutate(x => x.DrawImage(fitted, new Point(offsetX, offsetY), 1f));

            using var memoryStream = new MemoryStream();
            canvas.Save(memoryStream, new PngEncoder());
            normalizedBytes = memoryStream.ToArray();
            if (normalizedBytes.Length > MaximumByteLength)
            {
                throw InvalidImage("Anh chu ky sau khi chuan hoa vuot qua dung luong cho phep.");
            }
        }
        catch (Exception ex) when (ex is not BadRequestException)
        {
            throw InvalidImage("Dinh dang anh chu ky khong duoc ho tro hoac bi loi.");
        }

        return new ESignSignatureImage
        {
            Base64 = Convert.ToBase64String(normalizedBytes),
            MediaType = "image/png",
            ByteLength = normalizedBytes.Length,
            Width = TargetCanvasWidth,
            Height = TargetCanvasHeight,
            Sha256Hash = Convert.ToHexString(SHA256.HashData(normalizedBytes)).ToLowerInvariant()
        };
    }

    private static Rectangle FindSignatureBounds(Image<Rgba32> image)
    {
        var minX = image.Width;
        var minY = image.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (!IsSignaturePixel(image[x, y]))
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return new Rectangle(0, 0, image.Width, image.Height);
        }

        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static bool IsSignaturePixel(Rgba32 pixel)
    {
        if (pixel.A <= AlphaThreshold)
        {
            return false;
        }

        return pixel.R < WhiteThreshold ||
               pixel.G < WhiteThreshold ||
               pixel.B < WhiteThreshold;
    }

    private static string? ParseMediaType(string dataUrlHeader)
    {
        var semicolonIndex = dataUrlHeader.IndexOf(';');
        var mediaType = semicolonIndex > 5
            ? dataUrlHeader[5..semicolonIndex]
            : dataUrlHeader[5..];
        return mediaType.Trim().ToLowerInvariant();
    }

    private static bool IsSupportedMediaType(string? mediaType) =>
        NormalizeJpegMediaType(mediaType) is "image/png" or "image/jpeg";

    private static string? NormalizeJpegMediaType(string? mediaType) =>
        string.Equals(mediaType, "image/jpg", StringComparison.OrdinalIgnoreCase)
            ? "image/jpeg"
            : mediaType;

    private static string ResolveMediaType(IImageFormat format)
    {
        if (string.Equals(format.Name, "PNG", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        if (string.Equals(format.Name, "JPEG", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(format.Name, "JPG", StringComparison.OrdinalIgnoreCase))
        {
            return "image/jpeg";
        }

        throw InvalidImage("Dinh dang anh chu ky khong duoc ho tro hoac bi loi.");
    }

    private static BadRequestException InvalidImage(string message) =>
        new(ErrorCodes.ESignSignatureImageInvalid, message);
}
