using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Payments;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Payments;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions", table =>
        {
            table.HasCheckConstraint("ck_payment_transactions_amount_positive", "amount > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.WalletAccountId).HasColumnName("wallet_account_id").IsRequired();
        builder.Property(x => x.PayerUserId).HasColumnName("payer_user_id").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).HasDefaultValue("VND").IsRequired();
        builder.Property(x => x.PaymentPurpose).HasColumnName("payment_purpose").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ProviderOrderCode).HasColumnName("provider_order_code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProviderTransactionCode).HasColumnName("provider_transaction_code").HasMaxLength(100);
        builder.Property(x => x.ProviderCheckoutUrl).HasColumnName("provider_checkout_url").HasColumnType("text");
        builder.Property(x => x.ProviderQrCode).HasColumnName("provider_qr_code").HasColumnType("text");
        builder.Property(x => x.GatewayResponseCode).HasColumnName("gateway_response_code").HasMaxLength(100);
        builder.Property(x => x.GatewayResponseMessage).HasColumnName("gateway_response_message").HasColumnType("text");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.PaidAt).HasColumnName("paid_at");
        builder.Property(x => x.FailedAt).HasColumnName("failed_at");
        builder.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => x.ProviderOrderCode).IsUnique();
        builder.HasIndex(x => x.WalletAccountId);
        builder.HasIndex(x => x.PayerUserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.WalletAccount)
            .WithMany(x => x.PaymentTransactions)
            .HasForeignKey(x => x.WalletAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PayerUser)
            .WithMany()
            .HasForeignKey(x => x.PayerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
