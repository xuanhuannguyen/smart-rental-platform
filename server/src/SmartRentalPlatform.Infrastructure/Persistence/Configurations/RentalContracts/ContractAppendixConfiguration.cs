using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.RentalContracts;

public class ContractAppendixConfiguration : IEntityTypeConfiguration<ContractAppendix>
{
    public void Configure(EntityTypeBuilder<ContractAppendix> builder)
    {
        builder.ToTable("contract_appendices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RentalContractId).HasColumnName("contract_id").IsRequired();
        builder.Property(x => x.AppendixNumber).HasColumnName("appendix_number").HasMaxLength(50).IsRequired();
        builder.Property(x => x.EffectiveDate).HasColumnName("effective_date").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(x => x.ActivatedAt).HasColumnName("activated_at");
        builder.Property(x => x.StatusReason).HasColumnName("status_reason").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(x => x.RentalContract)
            .WithMany(x => x.Appendices)
            .HasForeignKey(x => x.RentalContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RentalContractId, x.AppendixNumber }).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
