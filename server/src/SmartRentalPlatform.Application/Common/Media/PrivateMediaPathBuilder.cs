namespace SmartRentalPlatform.Application.Common.Media;

public static class PrivateMediaPathBuilder
{
    public static string Build(Guid mediaAssetId, bool forceDownload = false)
    {
        var suffix = forceDownload ? "/download" : string.Empty;
        return $"/api/media/private/{mediaAssetId:D}{suffix}";
    }
}
