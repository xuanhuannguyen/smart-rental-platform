using System.Text.Json.Serialization;

namespace SmartRentalPlatform.Contracts.Responses.Kyc;

public class KycStatusResponse
{
    public bool HasSubmission { get; set; }

    public Guid? KycId { get; set; }

    public string? Status { get; set; }

    public string? EkycResult { get; set; }

    public string? RiskLevel { get; set; }

    public string? DocumentType { get; set; }

    public string? OcrFullName { get; set; }

    public string? OcrCitizenIdMasked { get; set; }

    public decimal? FaceMatchScore { get; set; }

    public string? LivenessResult { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [JsonPropertyName("rejected_reason")]
    public string? RejectedReason { get; set; }
}
