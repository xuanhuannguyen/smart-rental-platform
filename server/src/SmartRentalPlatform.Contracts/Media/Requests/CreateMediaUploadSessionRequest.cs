using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Contracts.Media.Requests;

public sealed class CreateMediaUploadSessionRequest
{
    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public MediaScope Scope { get; set; }

    public MediaVisibility Visibility { get; set; } = MediaVisibility.Private;
}
