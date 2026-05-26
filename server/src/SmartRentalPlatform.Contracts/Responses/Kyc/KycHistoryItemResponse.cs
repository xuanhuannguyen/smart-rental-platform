using System.Text.Json.Serialization;

namespace SmartRentalPlatform.Contracts.Responses.Kyc;

public class KycHistoryItemResponse
{
    public Guid KycId { get; set; }

    public string Status { get; set; } = default!;

    public string EkycResult { get; set; } = default!;

    public string RiskLevel { get; set; } = default!;

    public string DocumentType { get; set; } = default!;

    public string? OcrFullName { get; set; }

    public string? OcrCitizenIdMasked { get; set; }

    public decimal? FaceMatchScore { get; set; }

    public string? LivenessResult { get; set; }

    public DateTime SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [JsonPropertyName("rejected_reason")]
    public string? RejectedReason { get; set; }
}

public class KycHistoryResponse
{
    public List<KycHistoryItemResponse> Items { get; set; } = new();

    public int TotalItems { get; set; }
}
