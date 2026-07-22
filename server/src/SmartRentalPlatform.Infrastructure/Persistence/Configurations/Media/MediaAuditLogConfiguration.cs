using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Media;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Media;

public class MediaAuditLogConfiguration : IEntityTypeConfiguration<MediaAuditLog>
{
    public void Configure(EntityTypeBuilder<MediaAuditLog> builder)
    {
        builder.ToTable("media_audit_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MediaAssetId).HasColumnName("media_asset_id").IsRequired();
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
        builder.Property(x => x.Reason).HasColumnName("reason").HasColumnType("text");
        builder.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(x => x.MediaAsset)
            .WithMany(x => x.AuditLogs)
            .HasForeignKey(x => x.MediaAssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.MediaAssetId);
        builder.HasIndex(x => x.ActorUserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.MediaAssetId, x.CreatedAt });
    }
}
