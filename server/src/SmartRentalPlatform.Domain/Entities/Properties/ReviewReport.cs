using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using System;

namespace SmartRentalPlatform.Domain.Entities.Properties
{
    public class ReviewReport
    {
        public Guid Id { get; set; }
        public Guid RoomingHouseReviewId { get; set; }
        public Guid ReporterUserId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public ReportStatus Status { get; set; } = ReportStatus.Pending;
        public string? AdminNote { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }

        public RoomingHouseReview RoomingHouseReview { get; set; } = null!;
        public User ReporterUser { get; set; } = null!;
    }
}
