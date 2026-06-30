using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Rental;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Rental;

public class RentalRequestConfiguration : IEntityTypeConfiguration<RentalRequest>
{
    public void Configure(EntityTypeBuilder<RentalRequest> builder)
    {
        builder.ToTable("rental_requests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(x => x.TenantUserId).HasColumnName("tenant_user_id").IsRequired();
        builder.Property(x => x.ApprovedByLandlordId).HasColumnName("approved_by_landlord_id");
        builder.Property(x => x.DesiredStartDate).HasColumnName("desired_start_date").IsRequired();
        builder.Property(x => x.ExpectedEndDate).HasColumnName("expected_end_date").IsRequired();
        builder.Property(x => x.ExpectedOccupantCount).HasColumnName("expected_occupant_count").IsRequired();
        builder.Property(x => x.MonthlyRentSnapshot).HasColumnName("monthly_rent_snapshot").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.DepositAmountSnapshot).HasColumnName("deposit_amount_snapshot").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.TenantNote).HasColumnName("tenant_note").HasColumnType("text");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.RespondedAt).HasColumnName("responded_at");
        builder.Property(x => x.RejectedReason).HasColumnName("rejected_reason").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(x => x.Room)
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TenantUser)
            .WithMany(x => x.RentalRequests)
            .HasForeignKey(x => x.TenantUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByLandlord)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByLandlordId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.RoomId);
        builder.HasIndex(x => x.TenantUserId);
        builder.HasIndex(x => x.Status);
    }
}
