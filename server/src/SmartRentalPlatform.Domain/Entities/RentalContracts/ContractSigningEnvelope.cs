using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Domain.Entities.RentalContracts;

public class ContractSigningEnvelope
{
    public Guid Id { get; set; }
    
    // Tham chiếu đến Hợp đồng hoặc Phụ lục (Chỉ 1 trong 2 có giá trị)
    public Guid? RentalContractId { get; set; }
    public Guid? RentalContractAppendixId { get; set; }
    
    public ESignProvider Provider { get; set; }
    public string? ProviderEnvelopeId { get; set; }
    public SigningEnvelopeStatus Status { get; set; }
    public string? Title { get; set; }
    public string? UnsignedFileObjectKey { get; set; }
    public string? UnsignedFileSha256Hash { get; set; }
    public string? DocumentSnapshotEncrypted { get; set; }
    public string? DocumentSnapshotSha256Hash { get; set; }
    public string? DocumentTemplateVersion { get; set; }
    public DateTimeOffset? DocumentPreparedAt { get; set; }
    public string? SignedFileObjectKey { get; set; }
    public string? SignedFileSha256Hash { get; set; }
    public string? EvidenceFileObjectKey { get; set; }
    public string? ProviderStatusReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    

    
    // Navigations
    public virtual RentalContract? RentalContract { get; set; }
    public virtual ContractAppendix? ContractAppendix { get; set; }
    public virtual ICollection<ContractSignature> Signatures { get; set; } = new List<ContractSignature>();
}
