using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using System;

namespace SmartRentalPlatform.Infrastructure.Persistence.Configurations.Properties
{
    public class ViewingAppointmentConfiguration : IEntityTypeConfiguration<ViewingAppointment>
    {
        public void Configure(EntityTypeBuilder<ViewingAppointment> builder)
        {
            builder.ToTable("viewing_appointments");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.RoomId).HasColumnName("room_id").IsRequired();
            builder.Property(x => x.TenantUserId).HasColumnName("tenant_user_id").IsRequired();
            builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            builder.Property(x => x.ScheduledAt).HasColumnName("scheduled_at").IsRequired();
            builder.Property(x => x.DurationMinutes).HasColumnName("duration_minutes").HasDefaultValue(30).IsRequired();
            builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).HasDefaultValue(ViewingAppointmentStatus.Pending).IsRequired();
            builder.Property(x => x.TenantNote).HasColumnName("tenant_note").HasColumnType("text");
            builder.Property(x => x.LandlordNote).HasColumnName("landlord_note").HasColumnType("text");
            builder.Property(x => x.CancelReason).HasColumnName("cancel_reason").HasColumnType("text");
            builder.Property(x => x.RespondedAt).HasColumnName("responded_at");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()").IsRequired();

            builder.HasOne(x => x.Room).WithMany(x => x.ViewingAppointments).HasForeignKey(x => x.RoomId).
                OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.TenantUser).WithMany(x => x.TenantAppointments).HasForeignKey(x => x.TenantUserId).
                OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.CreatedByUser).WithMany(x => x.CreatedAppointments).HasForeignKey(x => x.CreatedByUserId).
                OnDelete(DeleteBehavior.Restrict);
        }
    }
}
