using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Wallets;
using SmartRentalPlatform.Domain.Enums.Billing;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Wallets;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("wallet_transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.WalletAccountId).HasColumnName("wallet_account_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.TransferGroupId).HasColumnName("transfer_group_id").IsRequired();
        builder.Property(x => x.TransactionType).HasColumnName("transaction_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Direction).HasColumnName("direction").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.BalanceBefore).HasColumnName("balance_before").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.BalanceAfter).HasColumnName("balance_after").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(WalletTransactionStatus.Pending)
            .IsRequired();

        builder.HasIndex(x => x.TransferGroupId);
        builder.HasOne(x => x.WalletAccount).WithMany(x => x.Transactions).HasForeignKey(x => x.WalletAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
