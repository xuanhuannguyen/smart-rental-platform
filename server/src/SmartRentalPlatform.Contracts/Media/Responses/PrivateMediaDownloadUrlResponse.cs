namespace SmartRentalPlatform.Contracts.Media.Responses;

public sealed record PrivateMediaDownloadUrlResponse(
    string Url,
    DateTimeOffset ExpiresAtUtc,
    string DeliveryMode);
