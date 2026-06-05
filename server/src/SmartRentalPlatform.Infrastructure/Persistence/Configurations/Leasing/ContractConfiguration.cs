using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Leasing;
using SmartRentalPlatform.Domain.Enums.Billing;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Leasing;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("contracts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RentalRequestId).HasColumnName("rental_request_id");
        builder.Property(x => x.RoomDepositId).HasColumnName("room_deposit_id");
        builder.Property(x => x.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(x => x.MainTenantUserId).HasColumnName("main_tenant_user_id").IsRequired();
        builder.Property(x => x.ContractNumber).HasColumnName("contract_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnName("end_date").IsRequired();
        builder.Property(x => x.MonthlyRent).HasColumnName("monthly_rent").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.DepositAmount).HasColumnName("deposit_amount").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.PaymentDay).HasColumnName("payment_day").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(ContractStatus.Draft)
            .IsRequired();
        builder.Property(x => x.RoomSnapshot).HasColumnName("room_snapshot").HasColumnType("jsonb");
        builder.Property(x => x.ActivatedAt).HasColumnName("activated_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.ContractNumber).IsUnique();
        builder.HasIndex(x => new { x.RoomId, x.Status });

        builder.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MainTenant).WithMany().HasForeignKey(x => x.MainTenantUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
