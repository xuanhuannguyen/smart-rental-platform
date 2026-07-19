using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Properties;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties
{
    public class RoomAmenityConfiguration : IEntityTypeConfiguration<RoomAmenity>
    {
        public void Configure(EntityTypeBuilder<RoomAmenity> builder)
        {
            builder.ToTable("room_amenities");
            builder.HasKey(x => new
            {
                x.RoomId,
                x.AmenityId
            });
            builder.Property(x => x.RoomId).HasColumnName("room_id");
            builder.Property(x => x.AmenityId).HasColumnName("amenity_id");
            builder.HasOne(x => x.Room).WithMany(x => x.RoomAmenities).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Amenity).WithMany(x => x.RoomAmenities).HasForeignKey(x => x.AmenityId).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.AmenityId, x.RoomId })
                .HasDatabaseName("ix_room_amenities_amenity_room");
        }
    }
}
