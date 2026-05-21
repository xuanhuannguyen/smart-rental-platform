using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities;
using SmartRentalPlatfrom.Domain.Entities;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.Property(x => x.PhoneNumber).HasMaxLength(13);
        builder.HasIndex(x => x.PhoneNumber).IsUnique();
        builder.Property(x => x.PasswordHash).HasMaxLength(500);
        builder.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AvatarObjectKey).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.OnboardingStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").IsRequired();

    }
}