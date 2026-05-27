namespace SmartRentalPlatform.Contracts.Kyc;

public sealed class VnptDocumentTestResponse
{
    public string? SessionId { get; set; }

    public string EkycResult { get; set; } = default!;

    public string? OcrFullName { get; set; }

    public string? OcrCitizenId { get; set; }

    public DateTime? OcrDateOfBirth { get; set; }

    public string? OcrGender { get; set; }

    public string? OcrAddress { get; set; }

    public decimal? OcrConfidence { get; set; }

    public string? DocumentCheckResult { get; set; }

    public string? FaceMatchResult { get; set; }

    public string? LivenessResult { get; set; }

    public string? RiskLevel { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsProviderFailure { get; set; }

    public bool IsDocumentUnreadable { get; set; }

    public string FrontImageObjectKey { get; set; } = default!;

    public string BackImageObjectKey { get; set; } = default!;
}
