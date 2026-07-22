namespace SmartRentalPlatform.Application.Common.Media;

public static class PublicMediaPathBuilder
{
    private const string PublicMediaVersion = "real-media-20260722";

    public static string Build(Guid mediaAssetId)
    {
        return $"/api/media/public/{mediaAssetId:D}?v={PublicMediaVersion}";
    }
}
