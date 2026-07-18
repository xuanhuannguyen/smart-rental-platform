namespace SmartRentalPlatform.Contracts.Media.Responses;

public sealed record MediaUploadSessionResponse(
    Guid MediaAssetId,
    string UploadUrl,
    string HttpMethod,
    string DeliveryMode,
    DateTimeOffset ExpiresAtUtc);
