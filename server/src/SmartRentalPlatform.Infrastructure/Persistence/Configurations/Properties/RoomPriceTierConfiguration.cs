using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Properties;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties
{
    public class RoomPriceTierConfiguration : IEntityTypeConfiguration<RoomPriceTier>
    {
        public void Configure(EntityTypeBuilder<RoomPriceTier> builder)
        {
            builder.ToTable("room_price_tiers");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.RoomId).HasColumnName("room_id").IsRequired();
            builder.Property(x => x.OccupantCount).HasColumnName("occupant_count").IsRequired();
            builder.Property(x => x.MonthlyRent).HasColumnName("monthly_rent").HasPrecision(12, 2).IsRequired();
            builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at") .IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            builder.HasOne(x => x.Room) .WithMany(x => x.PriceTiers).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
