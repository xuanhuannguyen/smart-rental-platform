using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Rental;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Rental;

public class RoomDepositConfiguration : IEntityTypeConfiguration<RoomDeposit>
{
    public void Configure(EntityTypeBuilder<RoomDeposit> builder)
    {
        builder.ToTable("room_deposits");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RentalRequestId).HasColumnName("rental_request_id").IsRequired();
        builder.Property(x => x.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(x => x.TenantUserId).HasColumnName("tenant_user_id").IsRequired();
        builder.Property(x => x.LandlordUserId).HasColumnName("landlord_user_id").IsRequired();
        builder.Property(x => x.DepositAmount).HasColumnName("deposit_amount").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10).HasDefaultValue("VND").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.PaymentDeadlineAt).HasColumnName("payment_deadline_at");
        builder.Property(x => x.PaidAt).HasColumnName("paid_at");
        builder.Property(x => x.RefundedAt).HasColumnName("refunded_at");
        builder.Property(x => x.ForfeitedAt).HasColumnName("forfeited_at");
        builder.Property(x => x.RefundAmount).HasColumnName("refund_amount").HasPrecision(12, 2);
        builder.Property(x => x.ForfeitedAmount).HasColumnName("forfeited_amount").HasPrecision(12, 2);
        builder.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        builder.Property(x => x.PaymentTransferGroupId).HasColumnName("payment_transfer_group_id");
        builder.Property(x => x.RefundTransferGroupId).HasColumnName("refund_transfer_group_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(x => x.RentalRequest)
            .WithOne(x => x.RoomDeposit)
            .HasForeignKey<RoomDeposit>(x => x.RentalRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Room)
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TenantUser)
            .WithMany()
            .HasForeignKey(x => x.TenantUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LandlordUser)
            .WithMany()
            .HasForeignKey(x => x.LandlordUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RentalRequestId).IsUnique();
        builder.HasIndex(x => x.RoomId);
        builder.HasIndex(x => x.TenantUserId);
        builder.HasIndex(x => x.LandlordUserId);
        builder.HasIndex(x => x.Status);
    }
}
