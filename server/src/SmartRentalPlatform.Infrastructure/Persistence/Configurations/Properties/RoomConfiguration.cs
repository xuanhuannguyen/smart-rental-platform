using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RoomingHouseId).HasColumnName("rooming_house_id").IsRequired();
        builder.Property(x => x.RoomNumber).HasColumnName("room_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Floor).HasColumnName("floor").IsRequired();
        builder.Property(x => x.AreaM2).HasColumnName("area_m2").HasPrecision(8, 2);
        builder.Property(x => x.MaxOccupants).HasColumnName("max_occupants").IsRequired();
        builder.Property(x => x.IsTieredPricing).HasColumnName("is_tiered_pricing").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(RoomStatus.Available)
            .IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.HasOne(x => x.RoomingHouse).WithMany(x => x.Rooms).HasForeignKey(x => x.RoomingHouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RoomingHouseId, x.Status, x.DeletedAt })
            .HasDatabaseName("ix_rooms_house_status_active");
        builder.HasIndex(x => new { x.RoomingHouseId, x.DeletedAt, x.Status, x.Floor, x.RoomNumber })
            .HasDatabaseName("ix_rooms_public_detail_order");
        builder.HasIndex(x => new { x.Status, x.DeletedAt, x.AreaM2, x.MaxOccupants })
            .HasDatabaseName("ix_rooms_public_filters");
    }
}
