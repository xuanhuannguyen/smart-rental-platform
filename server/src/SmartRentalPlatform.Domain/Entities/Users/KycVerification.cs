using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Domain.Entities.Users;

public class KycVerification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public KycDocumentType DocumentType { get; set; }
    public string FrontImageObjectKey { get; set; } = string.Empty;
    public string BackImageObjectKey { get; set; } = string.Empty;
    public string SelfieImageObjectKey { get; set; } = string.Empty;
    public string? OcrFullName { get; set; }
    public string? OcrCitizenIdMasked { get; set; }
    public string CitizenIdHash { get; set; } = string.Empty;
    public DateOnly? OcrDateOfBirth { get; set; }
    public string? OcrGender { get; set; }
    public string? OcrAddress { get; set; }
    public decimal? OcrConfidence { get; set; }
    public KycVerificationStatus Status { get; set; } = KycVerificationStatus.Pending;
    public Guid? ReviewedByAdminId { get; set; }
    public string? RejectedReason { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public User? ReviewedByAdmin { get; set; }
}
