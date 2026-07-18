using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties
{
    public class RoomingHouseLegalDocumentConfiguration : IEntityTypeConfiguration<RoomingHouseLegalDocument>
    {
        public void Configure(EntityTypeBuilder<RoomingHouseLegalDocument> builder)
        {
            builder.ToTable("rooming_house_legal_documents");
            builder.HasKey(x => x.RoomingHouseId);
            builder.Property(x => x.RoomingHouseId).HasColumnName("rooming_house_id");
            builder.Property(x => x.FrontMediaAssetId).HasColumnName("front_media_asset_id");
            builder.Property(x => x.BackMediaAssetId).HasColumnName("back_media_asset_id");
            builder.Property(x => x.ExtraMediaAssetId).HasColumnName("extra_media_asset_id");
            builder.Property(x => x.DocumentType).HasColumnName("document_type").HasConversion<string>()
                .HasMaxLength(50).HasDefaultValue(LegalDocumentType.LAND_USE_CERTIFICATE).IsRequired();
            builder.Property(x => x.DocumentNumberMasked).HasColumnName("document_number_masked").HasMaxLength(100).IsRequired();
            builder.Property(x => x.DocumentNumberHash).HasColumnName("document_number_hash").HasColumnType("text").IsRequired();
            builder.Property(x => x.UploadedAt).HasColumnName("uploaded_at").IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            builder.HasIndex(x => x.FrontMediaAssetId);
            builder.HasIndex(x => x.BackMediaAssetId);
            builder.HasIndex(x => x.ExtraMediaAssetId);
            builder.HasOne(x => x.RoomingHouse).WithOne(x => x.LegalDocument).HasForeignKey<RoomingHouseLegalDocument>(x => x.RoomingHouseId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.FrontMediaAssetId).OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.BackMediaAssetId).OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<MediaAsset>().WithMany().HasForeignKey(x => x.ExtraMediaAssetId).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
