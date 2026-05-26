using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Abstractions;

public interface IVnptEkycClient
{
    Task<VnptEkycClientResult> VerifyAsync(
        VnptEkycVerifyInput input,
        CancellationToken cancellationToken = default);
}

public sealed class VnptEkycVerifyInput
{
    public Guid UserId { get; init; }

    public string DocumentType { get; init; } = default!;

    public string FrontImageObjectKey { get; init; } = default!;

    public string BackImageObjectKey { get; init; } = default!;

    public string SelfieImageObjectKey { get; init; } = default!;

    public string SelfieCaptureMethod { get; init; } = default!;
}

public sealed class VnptEkycClientResult
{
    public string? SessionId { get; init; }

    public string EkycResult { get; init; } = default!;

    public string? OcrFullName { get; init; }

    public string? OcrCitizenId { get; init; }

    public DateTime? OcrDateOfBirth { get; init; }

    public string? OcrGender { get; init; }

    public string? OcrAddress { get; init; }

    public decimal? OcrConfidence { get; init; }

    public string? DocumentCheckResult { get; init; }

    public decimal? FaceMatchScore { get; init; }

    public string? FaceMatchResult { get; init; }

    public string? LivenessResult { get; init; }

    public KycRiskLevel? RiskLevel { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public bool IsProviderFailure { get; init; }

    public bool IsDocumentUnreadable { get; init; }
}
