namespace SmartRentalPlatform.Infrastructure.Options;

public class VietMapOptions
{
    public const string SectionName = "VietMap";

    public string BaseUrl { get; set; } = "https://maps.vietmap.vn";

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 20;

    public int DisplayType { get; set; } = 5;
}
