using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Enums.Billing;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Billing;

public class RoomingHouseServicePriceConfiguration : IEntityTypeConfiguration<RoomingHouseServicePrice>
{
    public void Configure(EntityTypeBuilder<RoomingHouseServicePrice> builder)
    {
        builder.ToTable("rooming_house_service_prices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RoomingHouseId).HasColumnName("rooming_house_id").IsRequired();
        builder.Property(x => x.ServiceTypeId).HasColumnName("service_type_id").IsRequired();
        builder.Property(x => x.BillingMethod).HasColumnName("billing_method").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.UnitName).HasColumnName("unit_name").HasMaxLength(50).IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.EffectiveFrom).HasColumnName("effective_from").IsRequired();
        builder.Property(x => x.EffectiveTo).HasColumnName("effective_to");
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => new { x.RoomingHouseId, x.ServiceTypeId, x.EffectiveFrom }).IsUnique();
        builder.HasIndex(x => new { x.RoomingHouseId, x.ServiceTypeId, x.IsActive });

        builder.HasOne(x => x.RoomingHouse)
            .WithMany()
            .HasForeignKey(x => x.RoomingHouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ServiceType)
            .WithMany(x => x.RoomingHouseServicePrices)
            .HasForeignKey(x => x.ServiceTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
