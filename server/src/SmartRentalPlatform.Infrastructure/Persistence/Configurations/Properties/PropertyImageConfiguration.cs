using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Properties;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties
{
    public class PropertyImageConfiguration : IEntityTypeConfiguration<PropertyImage>
    {
        public void Configure(EntityTypeBuilder<PropertyImage> builder)
        {
            builder.ToTable("property_images", table =>
            {
                table.HasCheckConstraint(
                    "ck_property_images_owner_exclusive",
                    "(rooming_house_id IS NOT NULL AND room_id IS NULL) OR (rooming_house_id IS NULL AND room_id IS NOT NULL)");
            });
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.RoomingHouseId).HasColumnName("rooming_house_id");
            builder.Property(x => x.RoomId).HasColumnName("room_id");
            builder.Property(x => x.ObjectKey).HasColumnName("object_key").HasColumnType("text").IsRequired();
            builder.Property(x => x.ImageUrl).HasColumnName("image_url").HasColumnType("text").IsRequired();
            builder.Property(x => x.Caption).HasColumnName("caption").HasMaxLength(255);
            builder.Property(x => x.IsCover).HasColumnName("is_cover").HasDefaultValue(false).IsRequired();
            builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0).IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.HasOne(x => x.RoomingHouse).WithMany(x => x.Images).HasForeignKey(x => x.RoomingHouseId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Room).WithMany(x => x.Images).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
