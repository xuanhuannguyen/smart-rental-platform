using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.RentalContracts;

public class ContractFileConfiguration : IEntityTypeConfiguration<ContractFile>
{
    public void Configure(EntityTypeBuilder<ContractFile> builder)
    {
        builder.ToTable("contract_files");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RentalContractId).HasColumnName("contract_id").IsRequired();
        builder.Property(x => x.RentalContractAppendixId).HasColumnName("appendix_id");
        builder.Property(x => x.StorageObjectKey).HasColumnName("storage_object_key").HasColumnType("text").IsRequired();
        builder.Property(x => x.FileVariant)
            .HasColumnName("file_variant")
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(ContractFileVariant.Raw)
            .IsRequired();
        builder.Property(x => x.FileUrl).HasColumnName("file_url").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(x => x.RentalContract)
            .WithMany(x => x.Files)
            .HasForeignKey(x => x.RentalContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RentalContractAppendix)
            .WithMany(x => x.Files)
            .HasForeignKey(x => x.RentalContractAppendixId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.RentalContractId);
        builder.HasIndex(x => x.RentalContractAppendixId);
        builder.HasIndex(x => new { x.RentalContractId, x.RentalContractAppendixId, x.FileVariant });
    }
}
