using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Payments;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Payments;

public class WithdrawalWebhookLogConfiguration : IEntityTypeConfiguration<WithdrawalWebhookLog>
{
    public void Configure(EntityTypeBuilder<WithdrawalWebhookLog> builder)
    {
        builder.ToTable("withdrawal_webhook_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.ProviderOrderCode)
            .HasColumnName("provider_order_code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();
            
        builder.HasIndex(x => new { x.WithdrawalRequestId, x.Status })
            .IsUnique();

        builder.HasOne<WithdrawalRequest>()
            .WithMany()
            .HasForeignKey(x => x.WithdrawalRequestId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.Property(x => x.WithdrawalRequestId).HasColumnName("withdrawal_request_id");
        builder.Property(x => x.Payload).HasColumnName("payload");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at");
    }
}
