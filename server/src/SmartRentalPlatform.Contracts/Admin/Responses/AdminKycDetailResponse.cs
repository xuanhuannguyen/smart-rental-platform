using System;

namespace SmartRentalPlatform.Contracts.Admin.Responses;

public class AdminKycDetailResponse : AdminKycListItemResponse
{
    public string DocumentType { get; set; } = string.Empty;
    public string EkycProvider { get; set; } = string.Empty;
    public string? EkycSessionId { get; set; }
    public DateTime? OcrDateOfBirth { get; set; }
    public string? OcrGender { get; set; }
    public string? OcrAddress { get; set; }
    public decimal? OcrConfidence { get; set; }
    public string? DocumentCheckResult { get; set; }
    public decimal? FaceMatchScore { get; set; }
    public string? FaceMatchResult { get; set; }
    public string? LivenessResult { get; set; }
    public string EkycResult { get; set; } = string.Empty;
    public string? EkycErrorCode { get; set; }
    public string? EkycErrorMessage { get; set; }
    public Guid? FrontMediaAssetId { get; set; }
    public Guid? BackMediaAssetId { get; set; }
    public Guid? SelfieMediaAssetId { get; set; }
    public string FrontImageUrl { get; set; } = string.Empty;
    public string BackImageUrl { get; set; } = string.Empty;
    public string SelfieImageUrl { get; set; } = string.Empty;
    public string? RejectedReason { get; set; }
    public Guid? ReviewedByAdminId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}
