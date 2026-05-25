using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Domain.Entities;

public class KycVerification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = default!;

    public DocumentType DocumentType { get; set; }

    public EkycProvider EkycProvider { get; set; }

    public string? EkycSessionId { get; set; }

    public string FrontImageObjectKey { get; set; } = default!;

    public string BackImageObjectKey { get; set; } = default!;

    public string SelfieImageObjectKey { get; set; } = default!;

    public SelfieCaptureMethod SelfieCaptureMethod { get; set; }

    public string? OcrFullName { get; set; }

    public string? OcrCitizenIdMasked { get; set; }

    public string? CitizenIdHash { get; set; }

    public DateOnly? OcrDateOfBirth { get; set; }

    public string? OcrGender { get; set; }

    public string? OcrAddress { get; set; }

    public decimal? OcrConfidence { get; set; }

    public DocumentCheckResult? DocumentCheckResult { get; set; }

    public decimal? FaceMatchScore { get; set; }

    public FaceMatchResult? FaceMatchResult { get; set; }

    public LivenessResult? LivenessResult { get; set; }

    public EkycResult EkycResult { get; set; }

    public string? EkycErrorCode { get; set; }

    public string? EkycErrorMessage { get; set; }

    public KycRiskLevel RiskLevel { get; set; }

    public KycSubmissionStatus Status { get; set; }

    public Guid? ReviewedByAdminId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectedReason { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
