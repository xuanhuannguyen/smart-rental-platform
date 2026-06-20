using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.RentalContracts;

public class RentalContractConfiguration : IEntityTypeConfiguration<RentalContract>
{
    public void Configure(EntityTypeBuilder<RentalContract> builder)
    {
        builder.ToTable("contracts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RentalRequestId).HasColumnName("rental_request_id").IsRequired();
        builder.Property(x => x.RoomDepositId).HasColumnName("room_deposit_id").IsRequired();
        builder.Property(x => x.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(x => x.MainTenantUserId).HasColumnName("main_tenant_user_id").IsRequired();
        builder.Property(x => x.ContractNumber).HasColumnName("contract_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnName("end_date").IsRequired();
        builder.Property(x => x.MonthlyRent).HasColumnName("monthly_rent").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.DepositAmount).HasColumnName("deposit_amount").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.PaymentDay).HasColumnName("payment_day").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.RoomSnapshot).HasColumnName("room_snapshot").HasColumnType("jsonb");
        builder.Property(x => x.SignatureDeadlineAt).HasColumnName("signature_deadline_at");
        builder.Property(x => x.ActivatedAt).HasColumnName("activated_at");
        builder.Property(x => x.TerminationDate).HasColumnName("termination_date");
        builder.Property(x => x.TerminationType).HasColumnName("termination_type").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.StatusReason).HasColumnName("status_reason").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(x => x.RentalRequest)
            .WithOne(x => x.RentalContract)
            .HasForeignKey<RentalContract>(x => x.RentalRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RoomDeposit)
            .WithOne(x => x.RentalContract)
            .HasForeignKey<RentalContract>(x => x.RoomDepositId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Room)
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MainTenantUser)
            .WithMany()
            .HasForeignKey(x => x.MainTenantUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RentalRequestId).IsUnique();
        builder.HasIndex(x => x.RoomDepositId).IsUnique();
        builder.HasIndex(x => x.ContractNumber).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
