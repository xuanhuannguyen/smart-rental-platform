using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Properties;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties
{
    public class RoomingHouseAmenityConfiguration : IEntityTypeConfiguration<RoomingHouseAmenity>
    {
        public void Configure(EntityTypeBuilder<RoomingHouseAmenity> builder)
        {
            builder.ToTable("rooming_house_amenities");
            builder.HasKey(x => new
            {
                x.RoomingHouseId,
                x.AmenityId
            });
            builder.Property(x => x.RoomingHouseId).HasColumnName("rooming_house_id");
            builder.Property(x => x.AmenityId).HasColumnName("amenity_id");
            builder.HasOne(x => x.RoomingHouse).WithMany(x => x.RoomingHouseAmenities).HasForeignKey(x => x.RoomingHouseId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Amenity).WithMany(x => x.RoomingHouseAmenities).HasForeignKey(x => x.AmenityId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
