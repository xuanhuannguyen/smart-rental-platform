using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Media;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Billing;

public class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.ToTable("meter_readings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RoomId).HasColumnName("room_id").IsRequired();
        builder.Property(x => x.ContractId).HasColumnName("contract_id").IsRequired();
        builder.Property(x => x.ServiceTypeId).HasColumnName("service_type_id").IsRequired();
        builder.Property(x => x.BillingPeriodStart).HasColumnName("billing_period_start").IsRequired();
        builder.Property(x => x.BillingPeriodEnd).HasColumnName("billing_period_end").IsRequired();
        builder.Property(x => x.PreviousReading).HasColumnName("previous_reading").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.CurrentReading).HasColumnName("current_reading").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.Consumption).HasColumnName("consumption").HasPrecision(12, 2).IsRequired();
        builder.Property(x => x.ProofMediaAssetId).HasColumnName("proof_media_asset_id");
        builder.Property(x => x.RecordedByLandlordUserId).HasColumnName("recorded_by_landlord_user_id").IsRequired();
        builder.Property(x => x.ReadingAt).HasColumnName("reading_at").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(x => new { x.ContractId, x.ServiceTypeId, x.BillingPeriodStart, x.BillingPeriodEnd });
        builder.HasIndex(x => x.ProofMediaAssetId);

        builder.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RentalContract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ServiceType).WithMany(x => x.MeterReadings).HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecordedByLandlord).WithMany().HasForeignKey(x => x.RecordedByLandlordUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProofMediaAsset).WithMany().HasForeignKey(x => x.ProofMediaAssetId).OnDelete(DeleteBehavior.SetNull);
    }
}
