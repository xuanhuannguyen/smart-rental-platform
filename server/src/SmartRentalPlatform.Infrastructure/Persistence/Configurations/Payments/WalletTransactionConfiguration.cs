using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Payments;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Payments;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("wallet_transactions", table =>
        {
            table.HasCheckConstraint("ck_wallet_transactions_amount_positive", "amount > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.WalletAccountId).HasColumnName("wallet_account_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.TransferGroupId).HasColumnName("transfer_group_id");
        builder.Property(x => x.TransactionType).HasColumnName("transaction_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Direction).HasColumnName("direction").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.BalanceBefore).HasColumnName("balance_before").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.BalanceAfter).HasColumnName("balance_after").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ReservedBalanceBefore).HasColumnName("reserved_balance_before").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ReservedBalanceAfter).HasColumnName("reserved_balance_after").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.RelatedEntityType).HasColumnName("related_entity_type").HasMaxLength(50);
        builder.Property(x => x.RelatedEntityId).HasColumnName("related_entity_id");
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.WalletAccountId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TransferGroupId);
        builder.HasIndex(x => new { x.RelatedEntityType, x.RelatedEntityId });
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.WalletAccount)
            .WithMany(x => x.WalletTransactions)
            .HasForeignKey(x => x.WalletAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
