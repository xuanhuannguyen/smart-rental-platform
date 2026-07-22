using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.RentalContracts;

public class ESignWebhookLogConfiguration : IEntityTypeConfiguration<ESignWebhookLog>
{
    public void Configure(EntityTypeBuilder<ESignWebhookLog> builder)
    {
        builder.ToTable("esign_webhook_logs");

        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id).HasColumnName("id");
        
        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
            
        builder.Property(x => x.SigningEnvelopeId).HasColumnName("signing_envelope_id");
        
        builder.Property(x => x.ProviderEventId).HasColumnName("provider_event_id").HasMaxLength(100);
        builder.Property(x => x.ProviderEnvelopeId).HasColumnName("provider_envelope_id").HasMaxLength(255);
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(255);
        
        builder.Property(x => x.RawPayload).HasColumnName("raw_payload").HasColumnType("text").IsRequired();
        builder.Property(x => x.RawPayloadHash).HasColumnName("raw_payload_hash").HasMaxLength(64).IsRequired();
        
        builder.Property(x => x.SignatureStatus)
            .HasColumnName("signature_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
            
        builder.Property(x => x.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
            
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at").IsRequired();
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        
        builder.HasIndex(x => x.IdempotencyKey);
        builder.HasIndex(x => new { x.Provider, x.ProviderEnvelopeId });
    }
}
