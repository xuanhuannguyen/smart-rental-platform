using System;

namespace SmartRentalPlatform.Contracts.ViewingAppointments.Requests
{
    public class CreateViewingAppointmentRequest
    {
        public Guid RoomId { get; set; }
        public DateTimeOffset ScheduledAt { get; set; }
        public int? DurationMinutes { get; set; }
        public string? TenantNote { get; set; }
    }
}
