using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities;
namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(255).IsRequired();
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasColumnType("text");
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.AvatarUrl).HasColumnName("avatar_url").HasColumnType("text");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.OnboardingStatus).HasColumnName("onboarding_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.EmailConfirmed).HasColumnName("email_confirmed").IsRequired();
        builder.Property(x => x.PhoneConfirmed).HasColumnName("phone_confirmed").IsRequired();
        builder.Property(x => x.AccessFailedCount).HasColumnName("access_failed_count").IsRequired();
        builder.Property(x => x.LockoutEndAt).HasColumnName("lockout_end_at");
        builder.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
    }
}