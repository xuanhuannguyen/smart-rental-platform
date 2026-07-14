namespace SmartRentalPlatform.Application.Common.Media;

public static class PublicMediaPathBuilder
{
    public static string Build(Guid mediaAssetId)
    {
        return $"/api/media/public/{mediaAssetId:D}";
    }
}
