using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Domain.Entities.RentalContracts
{
    public class ContractSignature
    {
        public Guid Id { get; set; }

        public Guid? RentalContractId { get; set; }

        public Guid? RentalContractAppendixId { get; set; }

        public Guid SignerUserId { get; set; }

        public ContractSignerRole SignerRole { get; set; }

        public ContractSignatureMethod SignatureMethod { get; set; }

        public ContractSignatureStatus Status { get; set; }

        public int SigningOrder { get; set; }

        public Guid? ContractSigningEnvelopeId { get; set; }

        public ESignProvider? Provider { get; set; }

        public string? ProviderEnvelopeId { get; set; }

        public string? ProviderParticipantId { get; set; }

        public string? SigningUrl { get; set; }

        public string? CertificateSerialNumber { get; set; }

        public string? CertificateSubject { get; set; }

        public string? CertificateIssuer { get; set; }

        public string? SignedFileSha256Hash { get; set; }

        public string? ProviderEvidenceJson { get; set; }

        public DateTimeOffset? NotifiedAt { get; set; }

        public DateTimeOffset? SignedAt { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public RentalContract? RentalContract { get; set; }

        public ContractAppendix? RentalContractAppendix { get; set; }

        public User SignerUser { get; set; } = null!;

        public ContractSigningEnvelope? ContractSigningEnvelope { get; set; }
    }
}
