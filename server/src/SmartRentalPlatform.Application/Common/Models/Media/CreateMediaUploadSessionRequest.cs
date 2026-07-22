using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Application.Common.Models.Media;

public sealed class CreateMediaUploadSessionRequest
{
    public string OriginalFileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public long FileSize { get; init; }

    public MediaScope Scope { get; init; }

    public MediaVisibility Visibility { get; init; } = MediaVisibility.Private;
}
