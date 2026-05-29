namespace SmartRentalPlatform.Contracts.Kyc.Responses;

public class KycSubmissionResponse
{
    public Guid KycId { get; set; }
    public string Status { get; set; } = default!;
    public string EkycResult { get; set; } = default!;
    public string RiskLevel { get; set; } = default!;
    public string DocumentType { get; set; } = default!;
    public string? OcrFullName { get; set; }
    public string? OcrCitizenIdMasked { get; set; }
    public DateTime? OcrDateOfBirth { get; set; }
    public string? OcrGender { get; set; }
    public string? OcrAddress { get; set; }
    public decimal? OcrConfidence { get; set; }
    public string? DocumentCheckResult { get; set; }
    public decimal? FaceMatchScore { get; set; }
    public string? FaceMatchResult { get; set; }
    public string? LivenessResult { get; set; }
    public string? EkycErrorCode { get; set; }
    public string? EkycErrorMessage { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public string Message { get; set; } = default!;
}
