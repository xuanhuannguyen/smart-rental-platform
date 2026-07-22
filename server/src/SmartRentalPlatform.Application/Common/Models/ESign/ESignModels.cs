using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.Common.Models.ESign;

public class CreateEnvelopeInput
{
    public string FileName { get; set; } = string.Empty;
    public Stream FileStream { get; set; } = Stream.Null;
    public string ReferenceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset? EffectiveDate { get; set; }
    public DateTimeOffset? ExpirationDate { get; set; }
    public IReadOnlyList<ESignSignerInput> Signers { get; set; } = [];

    /// <summary>
    /// Signature zone coordinates keyed by signer role ("Landlord", "Tenant"),
    /// computed by the PDF renderer via QuestPDF CaptureContentPosition.
    /// Both zones are required for VNPT envelope creation.
    /// </summary>
    public IReadOnlyDictionary<string, SignatureZone> SignatureZones { get; set; }
        = new Dictionary<string, SignatureZone>();
}

public class ESignSignerInput
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int SigningOrder { get; set; }
    public string SignerRole { get; set; } = string.Empty;
}

public class CreateEnvelopeResult
{
    public bool IsSuccess { get; set; }
    public string? ProviderEnvelopeId { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<ESignSignerResult> Signers { get; set; } = [];
}

public class ESignSignerResult
{
    public Guid UserId { get; set; }
    public string SignerRole { get; set; } = string.Empty;
    public string? SigningUrl { get; set; }
    public string? ProviderParticipantId { get; set; }
    public string? ProviderAccessCode { get; set; }
    public string? ProviderEvidenceJson { get; set; }
}

public enum ESignOtpMethod
{
    SmsOtp = 2,
    EmailOtp = 3
}

public class EnvelopeStatusResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ProviderEnvelopeId { get; set; }
    public SigningEnvelopeStatus Status { get; set; }
    public IReadOnlyList<ESignSignerStatusResult> Signers { get; set; } = [];
}

public class ESignSignerStatusResult
{
    public string? ProviderParticipantId { get; set; }
    public ContractSignatureStatus Status { get; set; }
    public DateTimeOffset? SignedAt { get; set; }
    public string? CertificateSerialNumber { get; set; }
    public string? CertificateSubject { get; set; }
    public string? CertificateIssuer { get; set; }
}

public class SendSignOtpResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ProviderCode { get; set; }
    public int? ProviderStatusCode { get; set; }
    public long? OtpId { get; set; }
    public long? HdctPhienKyId { get; set; }
    public int? ValiditySeconds { get; set; }
    public string? Destination { get; set; }
}

public class SubmitSignOtpResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ProviderCode { get; set; }
    public int? ProviderStatusCode { get; set; }
}

public sealed class ESignSignatureImage
{
    public required string Base64 { get; init; }
    public required string MediaType { get; init; }
    public required int ByteLength { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string Sha256Hash { get; init; }
}
