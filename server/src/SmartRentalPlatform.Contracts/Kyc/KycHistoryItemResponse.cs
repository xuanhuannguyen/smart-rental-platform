namespace SmartRentalPlatform.Contracts.Kyc;

public class KycHistoryItemResponse
{
    public Guid KycId { get; set; }
    public string Status { get; set; } = default!;
    public string EkycResult { get; set; } = default!;
    public string RiskLevel { get; set; } = default!;
    public string DocumentType { get; set; } = default!;
    public string? OcrFullName { get; set; }
    public string? OcrCitizenIdMasked { get; set; }
    public string? OcrAddress { get; set; }
    public decimal? FaceMatchScore { get; set; }
    public string? LivenessResult { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? RejectedReason { get; set; }
}
