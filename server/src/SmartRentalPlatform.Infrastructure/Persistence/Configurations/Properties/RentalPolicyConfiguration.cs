using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties
{
    public class RentalPolicyConfiguration : IEntityTypeConfiguration<RentalPolicy>
    {
        public void Configure(EntityTypeBuilder<RentalPolicy> builder)
        {
            builder.ToTable("rental_policies");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.RoomingHouseId).HasColumnName("rooming_house_id").IsRequired();
            builder.Property(x => x.MinRentalMonths).HasColumnName("min_rental_months").IsRequired();
            builder.Property(x => x.MaxRentalMonths).HasColumnName("max_rental_months").IsRequired();
            builder.Property(x => x.AllowShortTermRenewal).HasColumnName("allow_short_term_renewal").IsRequired();
            builder.Property(x => x.RenewalNoticeDays).HasColumnName("renewal_notice_days").IsRequired();
            builder.Property(x => x.DepositMonths).HasColumnName("deposit_months").HasPrecision(5, 2).IsRequired();
            builder.Property(x => x.DefaultPaymentDay).HasColumnName("default_payment_day").HasDefaultValue(5).IsRequired();
            builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            builder.ToTable(x =>
                x.HasCheckConstraint(
                    "ck_rental_policies_default_payment_day_range",
                    "default_payment_day >= 1 AND default_payment_day <= 28"));
            builder.HasIndex(x => x.RoomingHouseId).IsUnique();
            builder.HasOne(x => x.RoomingHouse).WithOne(x => x.RentalPolicy).HasForeignKey<RentalPolicy>(x => x.RoomingHouseId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
