using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Billing;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Billing;

public class BillingServiceTypeConfiguration : IEntityTypeConfiguration<BillingServiceType>
{
    public void Configure(EntityTypeBuilder<BillingServiceType> builder)
    {
        builder.ToTable("billing_service_types");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SupportsMeterReading).HasColumnName("supports_meter_reading").IsRequired();
        builder.Property(x => x.MeterUnitName).HasColumnName("meter_unit_name").HasMaxLength(30);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.Name).IsUnique();

        builder.HasData(
            new BillingServiceType
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000001"),
                Name = "Điện",
                SupportsMeterReading = true,
                MeterUnitName = "kWh",
                IsActive = true,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new BillingServiceType
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000002"),
                Name = "Nước",
                SupportsMeterReading = true,
                MeterUnitName = "m3",
                IsActive = true,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new BillingServiceType
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000003"),
                Name = "Internet",
                SupportsMeterReading = false,
                MeterUnitName = null,
                IsActive = true,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new BillingServiceType
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000004"),
                Name = "Rác",
                SupportsMeterReading = false,
                MeterUnitName = null,
                IsActive = true,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            });
    }
}
