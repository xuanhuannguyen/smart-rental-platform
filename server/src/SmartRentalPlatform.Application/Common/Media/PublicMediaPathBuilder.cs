namespace SmartRentalPlatform.Application.Common.Media;

public static class PublicMediaPathBuilder
{
    public static string Build(string objectKey)
    {
        var normalizedObjectKey = objectKey.Replace('\\', '/').Trim().TrimStart('/');
        return $"/api/media/public/{normalizedObjectKey}";
    }
}
