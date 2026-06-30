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
        builder.Property(x => x.SignatureText).HasColumnName("signature_text").HasColumnType("text");
        builder.Property(x => x.SignedAt).HasColumnName("signed_at").IsRequired();
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
