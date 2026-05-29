using System;

namespace SmartRentalPlatform.Contracts.Users.Responses;

public class UserSessionResponse
{
    public Guid Id { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsCurrentSession { get; set; }
}
