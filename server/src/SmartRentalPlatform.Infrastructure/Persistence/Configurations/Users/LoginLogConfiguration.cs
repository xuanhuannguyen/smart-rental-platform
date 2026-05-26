using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Users;

public class LoginLogConfiguration : IEntityTypeConfiguration<LoginLog>
{
    public void Configure(EntityTypeBuilder<LoginLog> builder)
    {
        builder.ToTable("login_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.EmailAttempted).HasColumnName("email_attempted").HasMaxLength(256).IsRequired();
        builder.Property(x => x.LoginProvider).HasColumnName("login_provider").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasColumnType("text");
        builder.Property(x => x.IsSuccess).HasColumnName("is_success").IsRequired();
        builder.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(255);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.EmailAttempted);

        builder.HasOne(x => x.User)
            .WithMany(x => x.LoginLogs)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
