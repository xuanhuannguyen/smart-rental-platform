using System;
using System.Collections.Generic;

namespace SmartRentalPlatform.Contracts.ViewingAppointments.Responses
{
    public class ConflictCheckResponse
    {
        public bool HasConflict { get; set; }
        public string? Message { get; set; }
        public List<ConflictingAppointmentDto> ConflictingAppointments { get; set; } = new();
    }

    public class ConflictingAppointmentDto
    {
        public Guid Id { get; set; }
        public DateTimeOffset ScheduledAt { get; set; }
        public int DurationMinutes { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string RoomingHouseName { get; set; } = string.Empty;
    }
}
