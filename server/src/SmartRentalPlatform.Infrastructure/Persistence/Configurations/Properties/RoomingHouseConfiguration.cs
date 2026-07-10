using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties
{
    public class RoomingHouseConfiguration : IEntityTypeConfiguration<RoomingHouse>
    {
        public void Configure(EntityTypeBuilder<RoomingHouse> builder)
        {
            builder.ToTable("rooming_houses");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.LandlordUserId).HasColumnName("landlord_user_id").IsRequired();
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
            builder.Property(x => x.AddressLine).HasColumnName("address_line").HasColumnType("text").IsRequired();
            builder.Property(x => x.WardCode).HasColumnName("ward_code").HasMaxLength(20).IsRequired();
            builder.Property(x => x.ProvinceCode).HasColumnName("province_code").HasMaxLength(20).IsRequired();
            builder.Property(x => x.AddressDisplay).HasColumnName("address_display").HasColumnType("text").IsRequired();
            builder.Property(x => x.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
            builder.Property(x => x.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
            builder.Property(x => x.GoogleMapUrl).HasColumnName("google_map_url").HasColumnType("text");
            builder.Property(x => x.ApprovalStatus).HasColumnName("approval_status")
                .HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(x => x.VisibilityStatus).HasColumnName("visibility_status")
                .HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(x => x.AverageRating).HasColumnName("average_rating").HasDefaultValue(0).IsRequired();
            builder.Property(x => x.TotalReviews).HasColumnName("total_reviews").HasDefaultValue(0).IsRequired();
            builder.Property(x => x.RejectedReason).HasColumnName("rejected_reason").HasColumnType("text");
            builder.Property(x => x.ReviewedByAdminId).HasColumnName("reviewed_by_admin_id");
            builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            builder.HasOne(x => x.Landlord).WithMany(x => x.RoomingHouses).HasForeignKey(x => x.LandlordUserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Ward).WithMany(x => x.RoomingHouses).HasForeignKey(x => x.WardCode).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Province).WithMany(x => x.RoomingHouses).HasForeignKey(x => x.ProvinceCode).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.ReviewedByAdmin).WithMany(x => x.ReviewedRoomingHouses).HasForeignKey(x => x.ReviewedByAdminId).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
