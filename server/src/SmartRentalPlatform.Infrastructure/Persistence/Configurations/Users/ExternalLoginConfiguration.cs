using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Users;

public class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("external_logins");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.ProviderUserId).HasColumnName("provider_user_id").HasMaxLength(255).IsRequired();
        builder.Property(x => x.ProviderEmail).HasColumnName("provider_email").HasMaxLength(256).IsRequired();
        builder.Property(x => x.ProviderDisplayName).HasColumnName("provider_display_name").HasMaxLength(150);
        builder.Property(x => x.ProviderAvatarUrl).HasColumnName("provider_avatar_url").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.LastLoginAt).HasColumnName("last_login_at");

        builder.HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();

        builder.HasOne(x => x.User)
            .WithMany(x => x.ExternalLogins)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
