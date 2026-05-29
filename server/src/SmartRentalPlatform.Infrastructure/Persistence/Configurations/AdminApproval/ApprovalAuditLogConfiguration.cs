using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.AdminApproval;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.AdminApproval;

public class ApprovalAuditLogConfiguration : IEntityTypeConfiguration<ApprovalAuditLog>
{
    public void Configure(EntityTypeBuilder<ApprovalAuditLog> builder)
    {
        builder.ToTable("approval_audit_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.AdminId).HasColumnName("admin_id").IsRequired();
        builder.Property(x => x.ApprovalType).HasColumnName("approval_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasColumnType("text");
        builder.Property(x => x.AdditionalInfo).HasColumnName("additional_info").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => new { x.ApprovalType, x.EntityId });
        builder.HasIndex(x => x.AdminId);
        builder.HasIndex(x => x.CreatedAt);
    }
}
