using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Contracts.Common;

namespace SmartRentalPlatform.UnitTests.ESign;

public class SignatureImageNormalizerTests
{
    [Fact]
    public void Normalize_PngDataUrl_ReturnsStandardCanvas()
    {
        var bytes = CreatePng(320, 120);

        var result = SignatureImageNormalizer.Normalize(
            $"data:image/png;base64,{Convert.ToBase64String(bytes)}");

        Assert.Equal("image/png", result.MediaType);
        Assert.Equal(720, result.Width);
        Assert.Equal(240, result.Height);
        Assert.Equal(64, result.Sha256Hash.Length);
        Assert.NotEqual(bytes, Convert.FromBase64String(result.Base64));
    }

    [Fact]
    public void Normalize_JpegDataUrl_ReturnsStandardCanvas()
    {
        var bytes = CreateJpeg(640, 240);

        var result = SignatureImageNormalizer.Normalize(
            $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}");

        Assert.Equal("image/png", result.MediaType);
        Assert.Equal(720, result.Width);
        Assert.Equal(240, result.Height);
    }

    [Fact]
    public void Normalize_DifferentInputSizes_ReturnSameCanvas()
    {
        var wide = SignatureImageNormalizer.Normalize(Convert.ToBase64String(CreatePng(960, 180)));
        var tall = SignatureImageNormalizer.Normalize(Convert.ToBase64String(CreatePng(180, 420)));

        Assert.Equal(wide.Width, tall.Width);
        Assert.Equal(wide.Height, tall.Height);
        Assert.Equal("image/png", wide.MediaType);
        Assert.Equal("image/png", tall.MediaType);
    }

    [Theory]
    [InlineData("image/webp")]
    [InlineData("image/svg+xml")]
    [InlineData("image/gif")]
    public void Normalize_UnsupportedImageType_IsRejected(string mediaType)
    {
        var bytes = Enumerable.Repeat((byte)0x41, 64).ToArray();

        var exception = Assert.Throws<BadRequestException>(() =>
            SignatureImageNormalizer.Normalize(
                $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}"));

        Assert.Equal(ErrorCodes.ESignSignatureImageInvalid, exception.ErrorCode);
    }

    [Fact]
    public void Normalize_MimeDoesNotMatchImageBytes_IsRejected()
    {
        var bytes = CreatePng(320, 120);

        var exception = Assert.Throws<BadRequestException>(() =>
            SignatureImageNormalizer.Normalize(
                $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}"));

        Assert.Equal(ErrorCodes.ESignSignatureImageInvalid, exception.ErrorCode);
    }

    [Fact]
    public void Normalize_ExcessiveDimensions_IsRejected()
    {
        var bytes = CreatePng(5000, 120);

        Assert.Throws<BadRequestException>(() =>
            SignatureImageNormalizer.Normalize(Convert.ToBase64String(bytes)));
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var image = CreateSignatureImage(width, height);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static byte[] CreateJpeg(int width, int height)
    {
        using var image = CreateSignatureImage(width, height);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder { Quality = 90 });
        return stream.ToArray();
    }

    private static Image<Rgba32> CreateSignatureImage(int width, int height)
    {
        var image = new Image<Rgba32>(width, height, Color.White);
        var startX = Math.Max(1, width / 5);
        var endX = Math.Max(startX + 1, width * 4 / 5);
        var midY = Math.Max(1, height / 2);
        var strokeHeight = Math.Max(2, height / 18);

        for (var y = Math.Max(0, midY - strokeHeight); y < Math.Min(height, midY + strokeHeight); y++)
        {
            for (var x = startX; x < endX; x++)
            {
                image[x, y] = new Rgba32(20, 20, 20, 255);
            }
        }

        return image;
    }
}
