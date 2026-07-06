using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Chat;
using SmartRentalPlatform.Domain.Enums.Chat;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Chat;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(x => x.RoomId).HasColumnName("room_id");
        builder.Property(x => x.DirectUserAId).HasColumnName("direct_user_a_id");
        builder.Property(x => x.DirectUserBId).HasColumnName("direct_user_b_id");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(x => x.LastMessageAt).HasColumnName("last_message_at");
        builder.Property(x => x.LastMessagePreview).HasColumnName("last_message_preview").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.IsClosed).HasColumnName("is_closed").HasDefaultValue(false).IsRequired();

        builder.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.DirectUserAId, x.DirectUserBId })
            .IsUnique()
            .HasFilter("\"type\" = 'Direct' AND \"direct_user_a_id\" IS NOT NULL AND \"direct_user_b_id\" IS NOT NULL")
            .HasDatabaseName("ux_conversations_direct_pair");
        builder.HasIndex(x => new { x.Type, x.RoomId }).HasDatabaseName("ix_conversations_type_room_id");
        builder.HasIndex(x => x.LastMessageAt).HasDatabaseName("ix_conversations_last_message_at");
    }
}
