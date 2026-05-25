using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations;

public class KycVerificationConfiguration : IEntityTypeConfiguration<KycVerification>
{
    public void Configure(EntityTypeBuilder<KycVerification> builder)
    {
        builder.ToTable("kyc_verifications");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.DocumentType).HasColumnName("document_type").HasMaxLength(30)
            .HasConversion<string>().IsRequired();
        builder.Property(x => x.EkycProvider).HasColumnName("ekyc_provider").HasMaxLength(30)
            .HasConversion<string>().IsRequired();
        builder.Property(x => x.EkycSessionId).HasColumnName("ekyc_session_id").HasMaxLength(100);

        builder.Property(x => x.FrontImageObjectKey).HasColumnName("front_image_object_key").IsRequired();
        builder.Property(x => x.BackImageObjectKey).HasColumnName("back_image_object_key").IsRequired();
        builder.Property(x => x.SelfieImageObjectKey).HasColumnName("selfie_image_object_key").IsRequired();
        builder.Property(x => x.SelfieCaptureMethod).HasColumnName("selfie_capture_method").HasMaxLength(30)
            .HasConversion<string>().IsRequired();

        builder.Property(x => x.OcrFullName).HasColumnName("ocr_full_name").HasMaxLength(255);
        builder.Property(x => x.OcrCitizenIdMasked).HasColumnName("ocr_citizen_id_masked").HasMaxLength(30);
        builder.Property(x => x.CitizenIdHash).HasColumnName("citizen_id_hash");
        builder.Property(x => x.OcrDateOfBirth).HasColumnName("ocr_date_of_birth");
        builder.Property(x => x.OcrGender).HasColumnName("ocr_gender").HasMaxLength(30);
        builder.Property(x => x.OcrAddress).HasColumnName("ocr_address");
        builder.Property(x => x.OcrConfidence).HasColumnName("ocr_confidence").HasPrecision(5, 4);

        builder.Property(x => x.DocumentCheckResult).HasColumnName("document_check_result").HasMaxLength(50)
            .HasConversion<string>();
        builder.Property(x => x.FaceMatchScore).HasColumnName("face_match_score").HasPrecision(5, 4);
        builder.Property(x => x.FaceMatchResult).HasColumnName("face_match_result").HasMaxLength(50)
            .HasConversion<string>();
        builder.Property(x => x.LivenessResult).HasColumnName("liveness_result").HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(x => x.EkycResult).HasColumnName("ekyc_result").HasMaxLength(50)
            .HasConversion<string>().IsRequired();
        builder.Property(x => x.EkycErrorCode).HasColumnName("ekyc_error_code").HasMaxLength(100);
        builder.Property(x => x.EkycErrorMessage).HasColumnName("ekyc_error_message");

        builder.Property(x => x.RiskLevel).HasColumnName("risk_level").HasMaxLength(30)
            .HasConversion<string>().IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(50)
            .HasConversion<string>().IsRequired();

        builder.Property(x => x.ReviewedByAdminId).HasColumnName("reviewed_by_admin_id");
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(x => x.RejectedReason).HasColumnName("rejected_reason");

        builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CitizenIdHash);
        builder.HasIndex(x => x.CreatedAt);
    }
}
