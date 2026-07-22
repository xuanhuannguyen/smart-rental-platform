using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Payments;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Payments;

public class WithdrawalRequestConfiguration : IEntityTypeConfiguration<WithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<WithdrawalRequest> builder)
    {
        builder.ToTable("withdrawal_requests");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Fee)
            .HasColumnName("fee")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ProviderOrderCode)
            .HasColumnName("provider_order_code")
            .HasMaxLength(100);

        builder.HasIndex(x => x.ProviderOrderCode)
            .IsUnique();

        builder.Property(x => x.BankBin)
            .HasColumnName("bank_bin")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.AccountName)
            .HasColumnName("account_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.AccountNumber)
            .HasColumnName("account_number")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(x => x.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique();

        builder.HasOne(x => x.WalletAccount)
            .WithMany()
            .HasForeignKey(x => x.WalletAccountId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.Property(x => x.WalletAccountId).HasColumnName("wallet_account_id");
        builder.Property(x => x.ProviderTransactionCode).HasColumnName("provider_transaction_code");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
