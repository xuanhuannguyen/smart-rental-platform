using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Users;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");

        builder.HasKey(x => x.UserId);

        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(150);
        builder.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
        builder.Property(x => x.Gender).HasColumnName("gender").HasMaxLength(30);
        builder.Property(x => x.AddressLine).HasColumnName("address_line").HasColumnType("text");
        builder.Property(x => x.VerifiedCitizenIdMasked).HasColumnName("verified_citizen_id_masked").HasMaxLength(50);
        builder.Property(x => x.EmergencyContactName).HasColumnName("emergency_contact_name").HasMaxLength(150);
        builder.Property(x => x.EmergencyContactPhone).HasColumnName("emergency_contact_phone").HasMaxLength(20);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.User)
            .WithOne(x => x.UserProfile)
            .HasForeignKey<UserProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
