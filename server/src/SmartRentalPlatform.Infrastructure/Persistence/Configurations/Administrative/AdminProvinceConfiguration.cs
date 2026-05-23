using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Infrastructure.Persistence.Seed;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Administrative
{
    public class AdminProvinceConfiguration : IEntityTypeConfiguration<AdministrativeProvince>
    {
        public void Configure(EntityTypeBuilder<AdministrativeProvince> builder)
        {
            builder.ToTable("administrative_provinces");
            builder.HasKey(x => x.Code);
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            builder.HasData(AdministrativeSeed.GetProvinces());
        }
    }
}
