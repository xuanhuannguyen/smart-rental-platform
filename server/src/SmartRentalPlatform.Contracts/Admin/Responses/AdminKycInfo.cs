using System;

namespace SmartRentalPlatform.Contracts.Admin.Responses;

public class AdminKycInfo
{
    public Guid KycId { get; set; }
    public string FrontImageUrl { get; set; } = string.Empty;
    public string BackImageUrl { get; set; } = string.Empty;
    public string SelfieImageUrl { get; set; } = string.Empty;
    public string? OcrFullName { get; set; }
    public string? OcrCitizenIdMasked { get; set; }
    public DateOnly? OcrDateOfBirth { get; set; }
    public string? OcrGender { get; set; }
    public string? OcrAddress { get; set; }
    public decimal? FaceMatchScore { get; set; }
    public string EkycResult { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}
