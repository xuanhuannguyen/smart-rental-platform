using System;

namespace SmartRentalPlatform.Contracts.Admin.Responses;

public class AdminKycListItemResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string? OcrFullName { get; set; }
    public string? OcrCitizenIdMasked { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; set; }
}
