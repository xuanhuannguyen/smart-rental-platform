using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Enums.Billing;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Billing;

public class BillingServiceTypeConfiguration : IEntityTypeConfiguration<BillingServiceType>
{
    public void Configure(EntityTypeBuilder<BillingServiceType> builder)
    {
        builder.ToTable("billing_service_types");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Code).HasColumnName("code").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsMetered).HasColumnName("is_metered").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasData(
            new BillingServiceType
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000001"),
                Code = BillingServiceCode.Electric,
                Name = "Electric",
                IsMetered = true,
                IsActive = true,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new BillingServiceType
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000002"),
                Code = BillingServiceCode.Water,
                Name = "Water",
                IsMetered = true,
                IsActive = true,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new BillingServiceType
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000003"),
                Code = BillingServiceCode.Wifi,
                Name = "Wifi",
                IsMetered = false,
                IsActive = true,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new BillingServiceType
            {
                Id = Guid.Parse("60000000-0000-0000-0000-000000000004"),
                Code = BillingServiceCode.Trash,
                Name = "Trash",
                IsMetered = false,
                IsActive = true,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            });
    }
}
