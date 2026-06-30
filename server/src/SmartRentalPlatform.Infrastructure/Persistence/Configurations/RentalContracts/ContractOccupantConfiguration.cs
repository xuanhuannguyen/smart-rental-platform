using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.RentalContracts;

public class ContractOccupantConfiguration : IEntityTypeConfiguration<ContractOccupant>
{
    public void Configure(EntityTypeBuilder<ContractOccupant> builder)
    {
        builder.ToTable("contract_occupants");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RentalContractId).HasColumnName("contract_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.GuardianOccupantId).HasColumnName("guardian_occupant_id");
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
        builder.Property(x => x.DateOfBirth).HasColumnName("date_of_birth").IsRequired();
        builder.Property(x => x.RelationshipToMainTenant).HasColumnName("relationship_to_main_tenant").HasMaxLength(100);
        builder.Property(x => x.MoveInDate).HasColumnName("move_in_date").IsRequired();
        builder.Property(x => x.MoveOutDate).HasColumnName("move_out_date");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(x => x.RentalContract)
            .WithMany(x => x.Occupants)
            .HasForeignKey(x => x.RentalContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.GuardianOccupant)
            .WithMany(x => x.Dependents)
            .HasForeignKey(x => x.GuardianOccupantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RentalContractId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.GuardianOccupantId);
        builder.HasIndex(x => x.Status);
    }
}
