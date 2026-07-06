using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Chat;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Chat;

public sealed class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.ToTable("conversation_participants");

        builder.HasKey(x => new { x.ConversationId, x.UserId });
        builder.Property(x => x.ConversationId).HasColumnName("conversation_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.AddedByUserId).HasColumnName("added_by_user_id");
        builder.Property(x => x.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.LeftAt).HasColumnName("left_at");
        builder.Property(x => x.LastReadAt).HasColumnName("last_read_at");
        builder.Property(x => x.UnreadCount).HasColumnName("unread_count").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.IsMuted).HasColumnName("is_muted").HasDefaultValue(false).IsRequired();

        builder.HasOne(x => x.Conversation).WithMany(x => x.Participants).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AddedByUser).WithMany().HasForeignKey(x => x.AddedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.LeftAt, x.UnreadCount }).HasDatabaseName("ix_conversation_participants_user_left_unread");
    }
}
