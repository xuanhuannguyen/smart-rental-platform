using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Users;

public class KycVerificationConfiguration : IEntityTypeConfiguration<KycVerification>
{
    public void Configure(EntityTypeBuilder<KycVerification> builder)
    {
        builder.ToTable("kyc_verifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.DocumentType).HasColumnName("document_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.EkycProvider).HasColumnName("ekyc_provider").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.EkycSessionId).HasColumnName("ekyc_session_id").HasMaxLength(100);
        builder.Property(x => x.FrontMediaAssetId).HasColumnName("front_media_asset_id");
        builder.Property(x => x.BackMediaAssetId).HasColumnName("back_media_asset_id");
        builder.Property(x => x.SelfieMediaAssetId).HasColumnName("selfie_media_asset_id");
        builder.Property(x => x.SelfieCaptureMethod).HasColumnName("selfie_capture_method").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.OcrFullName).HasColumnName("ocr_full_name").HasMaxLength(150);
        builder.Property(x => x.OcrCitizenIdMasked).HasColumnName("ocr_citizen_id_masked").HasMaxLength(50);
        builder.Property(x => x.CitizenIdHash).HasColumnName("citizen_id_hash").HasColumnType("text").IsRequired();
        builder.Property(x => x.DocumentNumberEncrypted).HasColumnName("document_number_encrypted").HasColumnType("text");
        builder.Property(x => x.OcrDateOfBirth).HasColumnName("ocr_date_of_birth");
        builder.Property(x => x.OcrGender).HasColumnName("ocr_gender").HasMaxLength(30);
        builder.Property(x => x.OcrAddress).HasColumnName("ocr_address").HasColumnType("text");
        builder.Property(x => x.OcrConfidence).HasColumnName("ocr_confidence").HasPrecision(5, 4);
        builder.Property(x => x.DocumentCheckResult).HasColumnName("document_check_result").HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.FaceMatchScore).HasColumnName("face_match_score").HasPrecision(5, 4);
        builder.Property(x => x.FaceMatchResult).HasColumnName("face_match_result").HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.LivenessResult).HasColumnName("liveness_result").HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.EkycResult).HasColumnName("ekyc_result").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.EkycErrorCode).HasColumnName("ekyc_error_code").HasMaxLength(100);
        builder.Property(x => x.EkycErrorMessage).HasColumnName("ekyc_error_message").HasColumnType("text");
        builder.Property(x => x.RiskLevel).HasColumnName("risk_level").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReviewedByAdminId).HasColumnName("reviewed_by_admin_id");
        builder.Property(x => x.RejectedReason).HasColumnName("rejected_reason").HasColumnType("text");
        builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at").IsRequired();
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CitizenIdHash);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.FrontMediaAssetId);
        builder.HasIndex(x => x.BackMediaAssetId);
        builder.HasIndex(x => x.SelfieMediaAssetId);

        builder.HasOne(x => x.User)
            .WithMany(x => x.KycVerifications)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReviewedByAdmin)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.FrontMediaAsset)
            .WithMany()
            .HasForeignKey(x => x.FrontMediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BackMediaAsset)
            .WithMany()
            .HasForeignKey(x => x.BackMediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SelfieMediaAsset)
            .WithMany()
            .HasForeignKey(x => x.SelfieMediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
