namespace SmartRentalPlatform.Application.Common.Media;

public static class AdminPrivateMediaPathBuilder
{
    public static string Build(Guid mediaAssetId, bool forceDownload = false)
    {
        var suffix = forceDownload ? "/download" : string.Empty;
        return $"/api/admin/media/private/{mediaAssetId:D}{suffix}";
    }
}
