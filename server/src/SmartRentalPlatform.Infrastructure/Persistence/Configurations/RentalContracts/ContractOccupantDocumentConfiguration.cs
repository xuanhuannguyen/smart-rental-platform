using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.RentalContracts;

public class ContractOccupantDocumentConfiguration : IEntityTypeConfiguration<ContractOccupantDocument>
{
    public void Configure(EntityTypeBuilder<ContractOccupantDocument> builder)
    {
        builder.ToTable("contract_occupant_documents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RentalContractOccupantId).HasColumnName("contract_occupant_id").IsRequired();
        builder.Property(x => x.DocumentType).HasColumnName("document_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.DocumentNumberMasked).HasColumnName("document_number_masked").HasMaxLength(100);
        builder.Property(x => x.DocumentNumberHash).HasColumnName("document_number_hash").HasColumnType("text");
        builder.Property(x => x.DocumentNumberEncrypted).HasColumnName("document_number_encrypted").HasColumnType("text");
        builder.Property(x => x.FrontMediaAssetId).HasColumnName("front_media_asset_id");
        builder.Property(x => x.BackMediaAssetId).HasColumnName("back_media_asset_id");
        builder.Property(x => x.ExtraMediaAssetId).HasColumnName("extra_media_asset_id");
        builder.Property(x => x.FrontImageObjectKey).HasColumnName("front_image_object_key").HasColumnType("text").IsRequired();
        builder.Property(x => x.BackImageObjectKey).HasColumnName("back_image_object_key").HasColumnType("text");
        builder.Property(x => x.ExtraImageObjectKey).HasColumnName("extra_image_object_key").HasColumnType("text");
        builder.Property(x => x.UploadedAt).HasColumnName("uploaded_at").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(x => x.RentalContractOccupant)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => x.RentalContractOccupantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FrontMediaAsset)
            .WithMany()
            .HasForeignKey(x => x.FrontMediaAssetId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.BackMediaAsset)
            .WithMany()
            .HasForeignKey(x => x.BackMediaAssetId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ExtraMediaAsset)
            .WithMany()
            .HasForeignKey(x => x.ExtraMediaAssetId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.RentalContractOccupantId).IsUnique();
        builder.HasIndex(x => x.DocumentNumberHash);
        builder.HasIndex(x => x.FrontMediaAssetId);
        builder.HasIndex(x => x.BackMediaAssetId);
        builder.HasIndex(x => x.ExtraMediaAssetId);
    }
}
