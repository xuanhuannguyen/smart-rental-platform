using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Users;

public class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.ToTable("user_tokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.TokenType).HasColumnName("token_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasColumnType("text").IsRequired();
        builder.Property(x => x.TokenFamilyId).HasColumnName("token_family_id").IsRequired();
        builder.Property(x => x.ReplacedByTokenId).HasColumnName("replaced_by_token_id");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.UsedAt).HasColumnName("used_at");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.RevokedReason).HasColumnName("revoked_reason").HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.CreatedByIp).HasColumnName("created_by_ip").HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.TokenFamilyId);

        builder.HasOne(x => x.User)
            .WithMany(x => x.UserTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReplacedByToken)
            .WithMany(x => x.ReplacingTokens)
            .HasForeignKey(x => x.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
