using System;
using SmartRentalPlatform.Domain.Entities.Users;

namespace SmartRentalPlatform.Domain.Entities.Properties
{
    public class ViewingAppointment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RoomId { get; set; }
        public Guid TenantUserId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public DateTimeOffset ScheduledAt { get; set; }
        public int DurationMinutes { get; set; } = 30;
        public ViewingAppointmentStatus Status { get; set; } = ViewingAppointmentStatus.Pending;
        public string? TenantNote { get; set; }
        public string? LandlordNote { get; set; }
        public string? CancelReason { get; set; }
        public DateTimeOffset? RespondedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        public Room Room { get; set; } = null!;
        public User TenantUser { get; set; } = null!;
        public User CreatedByUser { get; set; } = null!;
    }
}
