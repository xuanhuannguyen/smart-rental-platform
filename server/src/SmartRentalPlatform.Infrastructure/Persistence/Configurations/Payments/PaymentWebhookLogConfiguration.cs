using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Payments;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Payments;

public class PaymentWebhookLogConfiguration : IEntityTypeConfiguration<PaymentWebhookLog>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookLog> builder)
    {
        builder.ToTable("payment_webhook_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PaymentTransactionId).HasColumnName("payment_transaction_id");
        builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ProviderEventId).HasColumnName("provider_event_id").HasMaxLength(120);
        builder.Property(x => x.ProviderOrderCode).HasColumnName("provider_order_code").HasMaxLength(100);
        builder.Property(x => x.ProviderTransactionCode).HasColumnName("provider_transaction_code").HasMaxLength(100);
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(120);
        builder.Property(x => x.RawPayload).HasColumnName("raw_payload").HasColumnType("text").IsRequired();
        builder.Property(x => x.RawPayloadHash).HasColumnName("raw_payload_hash").HasColumnType("text").IsRequired();
        builder.Property(x => x.SignatureStatus).HasColumnName("signature_status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ProcessingStatus).HasColumnName("processing_status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasColumnType("text");
        builder.Property(x => x.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at").IsRequired();
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.RawPayloadHash).IsUnique();
        builder.HasIndex(x => x.PaymentTransactionId);
        builder.HasIndex(x => x.ProviderOrderCode);
        builder.HasIndex(x => x.ProcessingStatus);
        builder.HasIndex(x => x.ReceivedAt);

        builder.HasOne(x => x.PaymentTransaction)
            .WithMany(x => x.WebhookLogs)
            .HasForeignKey(x => x.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
