using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Chat;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Chat;

public sealed class ConversationJoinRequestConfiguration : IEntityTypeConfiguration<ConversationJoinRequest>
{
    public void Configure(EntityTypeBuilder<ConversationJoinRequest> builder)
    {
        builder.ToTable("conversation_join_requests");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ConversationId).HasColumnName("conversation_id");
        builder.Property(x => x.RequesterUserId).HasColumnName("requester_user_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");

        builder.HasOne(x => x.Conversation).WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.RequesterUser).WithMany().HasForeignKey(x => x.RequesterUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
