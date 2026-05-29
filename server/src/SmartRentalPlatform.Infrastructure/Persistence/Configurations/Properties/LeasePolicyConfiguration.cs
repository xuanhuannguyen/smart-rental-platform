using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties
{
    public class LeasePolicyConfiguration : IEntityTypeConfiguration<LeasePolicy>
    {
        public void Configure(EntityTypeBuilder<LeasePolicy> builder)
        {
            builder.ToTable("lease_policies");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.RoomingHouseId).HasColumnName("rooming_house_id").IsRequired();
            builder.Property(x => x.AllowShortTermRenewal).HasColumnName("allow_short_term_renewal").IsRequired();
            builder.Property(x => x.RenewalNoticeDays).HasColumnName("renewal_notice_days").IsRequired();
            builder.Property(x => x.DepositMonths).HasColumnName("deposit_months").HasPrecision(5, 2).IsRequired();
            builder.Property(x => x.Discount6MonthsPercent).HasColumnName("discount_6_months_percent").HasPrecision(5, 2).IsRequired();
            builder.Property(x => x.Discount9MonthsPercent).HasColumnName("discount_9_months_percent").HasPrecision(5, 2).IsRequired();
            builder.Property(x => x.Discount12MonthsPercent).HasColumnName("discount_12_months_percent").HasPrecision(5, 2).IsRequired();
            builder.Property(x => x.Discount24MonthsPercent).HasColumnName("discount_24_months_percent").HasPrecision(5, 2).IsRequired();
            builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            builder.HasIndex(x => x.RoomingHouseId).IsUnique();
            builder.HasOne(x => x.RoomingHouse).WithOne(x => x.LeasePolicy).HasForeignKey<LeasePolicy>(x => x.RoomingHouseId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
