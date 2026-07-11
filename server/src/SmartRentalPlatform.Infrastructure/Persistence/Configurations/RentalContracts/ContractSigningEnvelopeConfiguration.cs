using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.RentalContracts;

public class ContractSigningEnvelopeConfiguration : IEntityTypeConfiguration<ContractSigningEnvelope>
{
    public void Configure(EntityTypeBuilder<ContractSigningEnvelope> builder)
    {
        builder.ToTable("contract_signing_envelopes");

        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RentalContractId).HasColumnName("contract_id");
        builder.Property(x => x.RentalContractAppendixId).HasColumnName("appendix_id");
        
        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
            
        builder.Property(x => x.ProviderEnvelopeId)
            .HasColumnName("provider_envelope_id")
            .HasMaxLength(255);
            
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
            
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(1000);
        
        builder.Property(x => x.UnsignedFileObjectKey).HasColumnName("unsigned_file_object_key").HasColumnType("text");
        builder.Property(x => x.UnsignedFileSha256Hash).HasColumnName("unsigned_file_sha256_hash").HasMaxLength(64);
        builder.Property(x => x.DocumentSnapshotEncrypted).HasColumnName("document_snapshot_encrypted").HasColumnType("text");
        builder.Property(x => x.DocumentSnapshotSha256Hash).HasColumnName("document_snapshot_sha256_hash").HasMaxLength(64);
        builder.Property(x => x.DocumentTemplateVersion).HasColumnName("document_template_version").HasMaxLength(40);
        builder.Property(x => x.DocumentPreparedAt).HasColumnName("document_prepared_at");
        
        builder.Property(x => x.SignedFileObjectKey).HasColumnName("signed_file_object_key").HasColumnType("text");
        builder.Property(x => x.SignedFileSha256Hash).HasColumnName("signed_file_sha256_hash").HasMaxLength(64);
        
        builder.Property(x => x.EvidenceFileObjectKey).HasColumnName("evidence_file_object_key").HasColumnType("text");
        
        builder.Property(x => x.ProviderStatusReason).HasColumnName("provider_status_reason").HasMaxLength(2000);
        
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.SentAt).HasColumnName("sent_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");

        builder.HasOne(x => x.RentalContract)
            .WithMany(x => x.SigningEnvelopes)
            .HasForeignKey(x => x.RentalContractId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ContractAppendix)
            .WithMany(x => x.SigningEnvelopes)
            .HasForeignKey(x => x.RentalContractAppendixId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.Provider, x.ProviderEnvelopeId })
            .IsUnique()
            .HasFilter("provider_envelope_id IS NOT NULL");
            
        builder.HasIndex(x => new { x.RentalContractId, x.Status });
        builder.HasIndex(x => new { x.RentalContractAppendixId, x.Status });
    }
}
