using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Infrastructure.Persistence.Seed;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Administrative
{
    public class AdminDistrictConfiguration : IEntityTypeConfiguration<AdministrativeDistrict>
    {
        public void Configure(EntityTypeBuilder<AdministrativeDistrict> builder)
        {
            builder.ToTable("administrative_districts");
            builder.HasKey(x => x.Code);
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
            builder.Property(x => x.ProvinceCode).HasColumnName("province_code").HasMaxLength(20).IsRequired();
            builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            builder.HasOne(x => x.Province).WithMany(x => x.Districts).HasForeignKey(x => x.ProvinceCode).OnDelete(DeleteBehavior.Restrict);
            builder.HasData(AdministrativeSeed.GetDistricts());
        }
    }
}
