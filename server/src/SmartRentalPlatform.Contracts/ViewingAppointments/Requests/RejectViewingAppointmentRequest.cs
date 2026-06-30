using System;

namespace SmartRentalPlatform.Contracts.ViewingAppointments.Requests
{
    public class RejectViewingAppointmentRequest
    {
        public string RejectReason { get; set; } = string.Empty;
        public DateTimeOffset? ProposedScheduledAt { get; set; }
        public int? ProposedDurationMinutes { get; set; }
    }
}
