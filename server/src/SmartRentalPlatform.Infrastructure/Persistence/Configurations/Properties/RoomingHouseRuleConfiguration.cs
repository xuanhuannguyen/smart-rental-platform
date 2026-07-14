using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties;

public class RoomingHouseRuleConfiguration : IEntityTypeConfiguration<RoomingHouseRule>
{
    public void Configure(EntityTypeBuilder<RoomingHouseRule> builder)
    {
        builder.ToTable("rooming_house_rules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RoomingHouseId).HasColumnName("rooming_house_id").IsRequired();
        builder.Property(x => x.SourceType).HasColumnName("source_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.MediaAssetId).HasColumnName("media_asset_id");
        builder.Property(x => x.GeneralRules).HasColumnName("general_rules").HasColumnType("text");
        builder.Property(x => x.QuietHours).HasColumnName("quiet_hours").HasColumnType("text");
        builder.Property(x => x.SecurityPolicy).HasColumnName("security_policy").HasColumnType("text");
        builder.Property(x => x.CleaningPolicy).HasColumnName("cleaning_policy").HasColumnType("text");
        builder.Property(x => x.GuestPolicy).HasColumnName("guest_policy").HasColumnType("text");
        builder.Property(x => x.ParkingPolicy).HasColumnName("parking_policy").HasColumnType("text");
        builder.Property(x => x.UtilityPolicy).HasColumnName("utility_policy").HasColumnType("text");
        builder.Property(x => x.DamageCompensationPolicy).HasColumnName("damage_compensation_policy").HasColumnType("text");
        builder.Property(x => x.AdditionalNotes).HasColumnName("additional_notes").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(x => x.RoomingHouseId).IsUnique();
        builder.HasIndex(x => x.MediaAssetId);
        builder.HasOne(x => x.MediaAsset)
            .WithMany()
            .HasForeignKey(x => x.MediaAssetId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.RoomingHouse)
            .WithOne(x => x.HouseRule)
            .HasForeignKey<RoomingHouseRule>(x => x.RoomingHouseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
