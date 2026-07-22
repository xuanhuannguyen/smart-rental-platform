using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Properties;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties;

public class FavoriteRoomingHouseConfiguration : IEntityTypeConfiguration<FavoriteRoomingHouse>
{
    public void Configure(EntityTypeBuilder<FavoriteRoomingHouse> builder)
    {
        builder.ToTable("favorite_rooming_houses");

        // Khóa chính kép (Composite Key) đảm bảo không duplicate
        builder.HasKey(x => new { x.UserId, x.RoomingHouseId });

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.RoomingHouseId).HasColumnName("rooming_house_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RoomingHouse)
            .WithMany()
            .HasForeignKey(x => x.RoomingHouseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
