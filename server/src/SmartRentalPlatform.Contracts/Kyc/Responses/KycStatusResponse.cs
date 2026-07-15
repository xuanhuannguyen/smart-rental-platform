namespace SmartRentalPlatform.Contracts.Kyc.Responses;

public class KycStatusResponse
{
    public bool HasSubmission { get; set; }
    public Guid? KycId { get; set; }
    public string? Status { get; set; }
    public string? EkycResult { get; set; }
    public string? RiskLevel { get; set; }
    public string? DocumentType { get; set; }
    public Guid? FrontMediaAssetId { get; set; }
    public Guid? BackMediaAssetId { get; set; }
    public Guid? SelfieMediaAssetId { get; set; }
    public string? OcrFullName { get; set; }
    public string? OcrCitizenIdMasked { get; set; }
    public DateTime? OcrDateOfBirth { get; set; }
    public string? OcrGender { get; set; }
    public string? OcrAddress { get; set; }
    public decimal? FaceMatchScore { get; set; }
    public string? LivenessResult { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? RejectedReason { get; set; }
}
