using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Properties;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties
{
    public class ReviewReportConfiguration : IEntityTypeConfiguration<ReviewReport>
    {
        public void Configure(EntityTypeBuilder<ReviewReport> builder)
        {
            builder.ToTable("review_reports");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.RoomingHouseReviewId).HasColumnName("rooming_house_review_id").IsRequired();
            builder.Property(x => x.ReporterUserId).HasColumnName("reporter_user_id").IsRequired();
            builder.Property(x => x.Reason).HasColumnName("reason").HasColumnType("text").IsRequired();
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
            builder.Property(x => x.AdminNote).HasColumnName("admin_note").HasColumnType("text");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at");

            // Ràng buộc Unique: 1 Review + 1 Reporter chỉ được tạo 1 report
            builder.HasIndex(x => new { x.RoomingHouseReviewId, x.ReporterUserId })
                .IsUnique()
                .HasDatabaseName("uix_reports_review_reporter");

            builder.HasOne(x => x.RoomingHouseReview)
                .WithMany()
                .HasForeignKey(x => x.RoomingHouseReviewId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ReporterUser)
                .WithMany()
                .HasForeignKey(x => x.ReporterUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
