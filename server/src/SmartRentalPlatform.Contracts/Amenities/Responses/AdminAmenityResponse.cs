using System;

namespace SmartRentalPlatform.Contracts.Amenities.Responses;

public sealed record AdminAmenityResponse(
    int Id,
    string Name,
    string Scope,
    string? IconCode,
    bool IsActive,
    DateTimeOffset CreatedAt
);
