namespace SmartRentalPlatform.Application.Common.Models.Media;

public sealed class MediaAuditContext
{
    public string Action { get; init; } = string.Empty;

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string? Reason { get; init; }

    public string? MetadataJson { get; init; }
}
