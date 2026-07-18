using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Users;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(256).IsRequired();
        builder.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasColumnType("text");
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.AvatarUrl).HasColumnName("avatar_url").HasColumnType("text");
        builder.Property(x => x.AvatarMediaAssetId).HasColumnName("avatar_media_asset_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.OnboardingStatus).HasColumnName("onboarding_status").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.EmailConfirmed).HasColumnName("email_confirmed").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.PhoneConfirmed).HasColumnName("phone_confirmed").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.AccessFailedCount).HasColumnName("access_failed_count").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.LockoutEndAt).HasColumnName("lockout_end_at");
        builder.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.HasIndex(x => x.PhoneNumber);
        builder.HasIndex(x => x.AvatarMediaAssetId);

        builder.HasOne(x => x.AvatarMediaAsset)
            .WithMany()
            .HasForeignKey(x => x.AvatarMediaAssetId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
