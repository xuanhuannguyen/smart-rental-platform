using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.RentalContracts;

public class ContractAppendixChangeConfiguration : IEntityTypeConfiguration<ContractAppendixChange>
{
    public void Configure(EntityTypeBuilder<ContractAppendixChange> builder)
    {
        builder.ToTable("contract_appendix_changes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RentalContractAppendixId).HasColumnName("appendix_id").IsRequired();
        builder.Property(x => x.ChangeType).HasColumnName("change_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.TargetType).HasColumnName("target_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.TargetId).HasColumnName("target_id");
        builder.Property(x => x.FieldName).HasColumnName("field_name").HasMaxLength(100);
        builder.Property(x => x.OldValue).HasColumnName("old_value").HasColumnType("jsonb");
        builder.Property(x => x.NewValue).HasColumnName("new_value").HasColumnType("jsonb");
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(x => x.RentalContractAppendix)
            .WithMany(x => x.Changes)
            .HasForeignKey(x => x.RentalContractAppendixId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.RentalContractAppendixId);
        builder.HasIndex(x => new { x.RentalContractAppendixId, x.SortOrder });
    }
}
