using System;

namespace SmartRentalPlatform.Contracts.Administrative.Responses;

public sealed record AdminProvinceResponse(
    string Code,
    string Name,
    string Type,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
