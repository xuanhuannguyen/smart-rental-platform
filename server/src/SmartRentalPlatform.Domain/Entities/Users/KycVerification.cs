using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Entities.Media;

namespace SmartRentalPlatform.Domain.Entities.Users;

public class KycVerification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public KycDocumentType DocumentType { get; set; }
    public EkycProvider EkycProvider { get; set; } = EkycProvider.VNPT;
    public string? EkycSessionId { get; set; }
    public Guid? FrontMediaAssetId { get; set; }
    public Guid? BackMediaAssetId { get; set; }
    public Guid? SelfieMediaAssetId { get; set; }
    public SelfieCaptureMethod SelfieCaptureMethod { get; set; } = SelfieCaptureMethod.Upload;
    public string? OcrFullName { get; set; }
    public string? OcrCitizenIdMasked { get; set; }
    public string CitizenIdHash { get; set; } = string.Empty;
    public string? DocumentNumberEncrypted { get; set; }
    public DateOnly? OcrDateOfBirth { get; set; }
    public string? OcrGender { get; set; }
    public string? OcrAddress { get; set; }
    public decimal? OcrConfidence { get; set; }
    public DocumentCheckResult? DocumentCheckResult { get; set; }
    public decimal? FaceMatchScore { get; set; }
    public FaceMatchResult? FaceMatchResult { get; set; }
    public LivenessResult? LivenessResult { get; set; }
    public EkycResult EkycResult { get; set; } = EkycResult.ProviderError;
    public string? EkycErrorCode { get; set; }
    public string? EkycErrorMessage { get; set; }
    public KycRiskLevel RiskLevel { get; set; } = KycRiskLevel.High;
    public KycVerificationStatus Status { get; set; } = KycVerificationStatus.Pending;
    public Guid? ReviewedByAdminId { get; set; }
    public string? RejectedReason { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public User? ReviewedByAdmin { get; set; }
    public MediaAsset? FrontMediaAsset { get; set; }
    public MediaAsset? BackMediaAsset { get; set; }
    public MediaAsset? SelfieMediaAsset { get; set; }
}
