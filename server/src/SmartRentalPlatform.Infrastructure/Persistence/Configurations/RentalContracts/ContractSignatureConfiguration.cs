using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.RentalContracts;

public class ContractSignatureConfiguration : IEntityTypeConfiguration<ContractSignature>
{
    public void Configure(EntityTypeBuilder<ContractSignature> builder)
    {
        builder.ToTable("contract_signatures", table =>
        {
            table.HasCheckConstraint(
                "ck_contract_signatures_target_exclusive",
                "(contract_id IS NOT NULL AND appendix_id IS NULL) OR (contract_id IS NULL AND appendix_id IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RentalContractId).HasColumnName("contract_id");
        builder.Property(x => x.RentalContractAppendixId).HasColumnName("appendix_id");
        builder.Property(x => x.SignerUserId).HasColumnName("signer_user_id").IsRequired();
        builder.Property(x => x.SignerRole).HasColumnName("signer_role").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.SignatureMethod).HasColumnName("signature_method").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.SigningOrder).HasColumnName("signing_order").IsRequired();
        builder.Property(x => x.ContractSigningEnvelopeId).HasColumnName("signing_envelope_id");
        builder.Property(x => x.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.ProviderEnvelopeId).HasColumnName("provider_envelope_id").HasMaxLength(255);
        builder.Property(x => x.ProviderParticipantId).HasColumnName("provider_participant_id").HasMaxLength(255);
        builder.Property(x => x.SigningUrl).HasColumnName("signing_url").HasMaxLength(2000);
        builder.Property(x => x.CertificateSerialNumber).HasColumnName("certificate_serial_number").HasMaxLength(255);
        builder.Property(x => x.CertificateSubject).HasColumnName("certificate_subject").HasMaxLength(1000);
        builder.Property(x => x.CertificateIssuer).HasColumnName("certificate_issuer").HasMaxLength(1000);
        builder.Property(x => x.SignedFileSha256Hash).HasColumnName("signed_file_sha256_hash").HasMaxLength(64);
        builder.Property(x => x.ProviderEvidenceJson).HasColumnName("provider_evidence_json").HasColumnType("jsonb");
        builder.Property(x => x.NotifiedAt).HasColumnName("notified_at");
        builder.Property(x => x.SignedAt).HasColumnName("signed_at");
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(100);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(x => x.RentalContract)
            .WithMany(x => x.Signatures)
            .HasForeignKey(x => x.RentalContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RentalContractAppendix)
            .WithMany(x => x.Signatures)
            .HasForeignKey(x => x.RentalContractAppendixId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SignerUser)
            .WithMany()
            .HasForeignKey(x => x.SignerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ContractSigningEnvelope)
            .WithMany(x => x.Signatures)
            .HasForeignKey(x => x.ContractSigningEnvelopeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.RentalContractId);
        builder.HasIndex(x => x.RentalContractAppendixId);
        builder.HasIndex(x => x.SignerUserId);
        builder.HasIndex(x => new { x.RentalContractId, x.SignerRole })
            .IsUnique()
            .HasFilter("contract_id IS NOT NULL");
        builder.HasIndex(x => new { x.RentalContractAppendixId, x.SignerRole })
            .IsUnique()
            .HasFilter("appendix_id IS NOT NULL");
    }
}
