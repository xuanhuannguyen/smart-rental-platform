using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Enums.Billing;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Billing;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ContractId).HasColumnName("contract_id").IsRequired();
        builder.Property(x => x.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(x => x.TenantUserId).HasColumnName("tenant_user_id").IsRequired();
        builder.Property(x => x.LandlordUserId).HasColumnName("landlord_user_id").IsRequired();
        builder.Property(x => x.InvoiceNo).HasColumnName("invoice_no").HasMaxLength(50).IsRequired();
        builder.Property(x => x.BillingPeriodStart).HasColumnName("billing_period_start").IsRequired();
        builder.Property(x => x.BillingPeriodEnd).HasColumnName("billing_period_end").IsRequired();
        builder.Property(x => x.IssueDate).HasColumnName("issue_date");
        builder.Property(x => x.DueDate).HasColumnName("due_date").IsRequired();
        builder.Property(x => x.RentAmount).HasColumnName("rent_amount").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.UtilityAmount).HasColumnName("utility_amount").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.ServiceAmount).HasColumnName("service_amount").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.DiscountAmount).HasColumnName("discount_amount").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(InvoiceStatus.Draft)
            .IsRequired();
        builder.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        builder.Property(x => x.SentAt).HasColumnName("sent_at");
        builder.Property(x => x.PaidAt).HasColumnName("paid_at");
        builder.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(x => x.CancelReason).HasColumnName("cancel_reason").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => x.InvoiceNo).IsUnique();
        builder.HasIndex(x => new { x.ContractId, x.BillingPeriodStart, x.BillingPeriodEnd })
            .IsUnique()
            .HasFilter("\"status\" <> 'Cancelled'");

        builder.HasOne(x => x.RentalContract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Landlord).WithMany().HasForeignKey(x => x.LandlordUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
