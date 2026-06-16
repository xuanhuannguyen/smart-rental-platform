using System;

namespace SmartRentalPlatform.Contracts.ViewingAppointments.Responses
{
    public class ViewingAppointmentResponse
    {
        public Guid Id { get; set; }
        public Guid RoomId { get; set; }
        public Guid TenantUserId { get; set; }
        public Guid CreatedByUserId { get; set; }
        public DateTimeOffset ScheduledAt { get; set; }
        public int DurationMinutes { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? TenantNote { get; set; }
        public string? LandlordNote { get; set; }
        public string? CancelReason { get; set; }
        public DateTimeOffset? RespondedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        // Enriched fields (populated from navigation properties)
        public string? RoomNumber { get; set; }
        public string? RoomingHouseName { get; set; }
        public string? TenantDisplayName { get; set; }
    }
}
