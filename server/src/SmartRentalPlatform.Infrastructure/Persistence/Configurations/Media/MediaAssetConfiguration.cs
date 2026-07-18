using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums.Media;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Media;

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OwnerUserId).HasColumnName("owner_user_id");
        builder.Property(x => x.BucketName).HasColumnName("bucket_name").HasColumnType("text").IsRequired();
        builder.Property(x => x.ObjectKey).HasColumnName("object_key").HasColumnType("text").IsRequired();
        builder.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasColumnType("text").IsRequired();
        builder.Property(x => x.StoredFileName).HasColumnName("stored_file_name").HasColumnType("text").IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasColumnType("text").IsRequired();
        builder.Property(x => x.FileSize).HasColumnName("file_size").IsRequired();
        builder.Property(x => x.FileHash).HasColumnName("file_hash").HasColumnType("text");
        builder.Property(x => x.Scope)
            .HasColumnName("scope")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(x => x.Visibility)
            .HasColumnName("visibility")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(MediaVisibility.Private)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(MediaStatus.PendingUpload)
            .IsRequired();
        builder.Property(x => x.LinkedEntityType).HasColumnName("linked_entity_type").HasColumnType("text");
        builder.Property(x => x.LinkedEntityId).HasColumnName("linked_entity_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(x => x.ObjectKey).IsUnique();
        builder.HasIndex(x => x.OwnerUserId);
        builder.HasIndex(x => new { x.LinkedEntityType, x.LinkedEntityId });
        builder.HasIndex(x => new { x.Scope, x.Status });
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.DeletedAt);
    }
}
