using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Properties;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties
{
    public class RoomingHouseReviewConfiguration : IEntityTypeConfiguration<RoomingHouseReview>
    {
        public void Configure(EntityTypeBuilder<RoomingHouseReview> builder)
        {
            builder.ToTable("rooming_house_reviews");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.RoomingHouseId).HasColumnName("rooming_house_id").IsRequired();
            builder.Property(x => x.TenantUserId).HasColumnName("tenant_user_id").IsRequired();
            builder.Property(x => x.RentalContractId).HasColumnName("rental_contract_id").IsRequired();
            builder.Property(x => x.Rating).HasColumnName("rating").IsRequired();
            builder.Property(x => x.Comment).HasColumnName("comment").HasColumnType("text");
            builder.Property(x => x.LandlordReply).HasColumnName("landlord_reply").HasColumnType("text");
            builder.Property(x => x.LandlordReplyCreatedAt).HasColumnName("landlord_reply_created_at");
            builder.Property(x => x.IsHidden).HasColumnName("is_hidden").HasDefaultValue(false).IsRequired();
            builder.Property(x => x.ModerationStatus).HasColumnName("moderation_status").HasConversion<string>().HasMaxLength(32).HasDefaultValue(SmartRentalPlatform.Domain.Enums.Properties.RoomingHouseReviewModerationStatus.Approved).IsRequired();
            builder.Property(x => x.ModerationReason).HasColumnName("moderation_reason").HasColumnType("text");
            builder.Property(x => x.AiModerationProvider).HasColumnName("ai_moderation_provider").HasMaxLength(32);
            builder.Property(x => x.AiModerationRiskLevel).HasColumnName("ai_moderation_risk_level").HasMaxLength(32);
            builder.Property(x => x.AiModerationCategories).HasColumnName("ai_moderation_categories").HasColumnType("text");
            builder.Property(x => x.AiModerationJson).HasColumnName("ai_moderation_json").HasColumnType("text");
            builder.Property(x => x.AiReviewedAt).HasColumnName("ai_reviewed_at");
            builder.Property(x => x.ReviewedByAdminId).HasColumnName("reviewed_by_admin_id");
            builder.Property(x => x.AdminReviewedAt).HasColumnName("admin_reviewed_at");
            builder.Property(x => x.AdminNote).HasColumnName("admin_note").HasColumnType("text");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            // Ràng buộc Unique: 1 Contract + 1 Tenant chỉ được viết 1 review
            builder.HasIndex(x => new { x.RentalContractId, x.TenantUserId })
                .IsUnique()
                .HasDatabaseName("uix_reviews_contract_tenant");
            builder.HasIndex(x => new { x.RoomingHouseId, x.IsHidden, x.ModerationStatus, x.CreatedAt })
                .HasDatabaseName("ix_rooming_house_reviews_public_thread");
            builder.HasIndex(x => new { x.ModerationStatus, x.AiReviewedAt, x.CreatedAt })
                .HasDatabaseName("ix_rooming_house_reviews_ai_queue");

            builder.HasOne(x => x.RoomingHouse)
                .WithMany()
                .HasForeignKey(x => x.RoomingHouseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.TenantUser)
                .WithMany()
                .HasForeignKey(x => x.TenantUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ReviewedByAdmin)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.RentalContract)
                .WithMany()
                .HasForeignKey(x => x.RentalContractId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
