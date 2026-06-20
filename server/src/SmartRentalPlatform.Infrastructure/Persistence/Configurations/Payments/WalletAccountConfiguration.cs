using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Enums.Payments;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Payments;

public class WalletAccountConfiguration : IEntityTypeConfiguration<WalletAccount>
{
    public void Configure(EntityTypeBuilder<WalletAccount> builder)
    {
        builder.ToTable("wallet_accounts", table =>
        {
            table.HasCheckConstraint("ck_wallet_accounts_balance_non_negative", "balance >= 0");
            table.HasCheckConstraint("ck_wallet_accounts_reserved_balance_non_negative", "reserved_balance >= 0");
            table.HasCheckConstraint("ck_wallet_accounts_reserved_balance_lte_balance", "reserved_balance <= balance");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Balance).HasColumnName("balance").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ReservedBalance).HasColumnName("reserved_balance").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).HasDefaultValue("VND").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(WalletAccountStatus.Active)
            .IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
