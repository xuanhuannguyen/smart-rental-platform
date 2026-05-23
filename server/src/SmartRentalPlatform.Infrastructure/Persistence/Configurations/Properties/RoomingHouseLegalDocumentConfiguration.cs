using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
            builder.Property(x => x.DocumentType).HasColumnName("document_type").HasConversion<string>()
                .HasMaxLength(50).HasDefaultValue(LegalDocumentType.LAND_USE_CERTIFICATE).IsRequired();
            builder.Property(x => x.FrontImageObjectKey).HasColumnName("front_image_object_key").HasColumnType("text").IsRequired();
            builder.Property(x => x.BackImageObjectKey).HasColumnName("back_image_object_key").HasColumnType("text").IsRequired();
            builder.Property(x => x.ExtraImageObjectKey).HasColumnName("extra_image_object_key").HasColumnType("text");
            builder.Property(x => x.DocumentNumberMasked).HasColumnName("document_number_masked").HasMaxLength(100).IsRequired();
            builder.Property(x => x.DocumentNumberHash).HasColumnName("document_number_hash").HasColumnType("text").IsRequired();
            builder.Property(x => x.UploadedAt).HasColumnName("uploaded_at").IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            builder.HasOne(x => x.RoomingHouse).WithOne(x => x.LegalDocument).HasForeignKey<RoomingHouseLegalDocument>(x => x.RoomingHouseId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
