using System;

namespace SmartRentalPlatform.Contracts.Administrative.Responses;

public sealed record AdminWardResponse(
    string Code,
    string ProvinceCode,
    string Name,
    string Type,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
