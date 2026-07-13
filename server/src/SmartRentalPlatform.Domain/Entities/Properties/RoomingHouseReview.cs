using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.Properties;
using System;
using System.Collections.Generic;

namespace SmartRentalPlatform.Domain.Entities.Properties
{
    public class RoomingHouseReview
    {
        public Guid Id { get; set; }
        public Guid RoomingHouseId { get; set; }
        public Guid TenantUserId { get; set; }
        public Guid RentalContractId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public string? LandlordReply { get; set; }
        public DateTimeOffset? LandlordReplyCreatedAt { get; set; }
        public bool IsHidden { get; set; } = false;
        public RoomingHouseReviewModerationStatus ModerationStatus { get; set; } = RoomingHouseReviewModerationStatus.PendingAiReview;
        public string? ModerationReason { get; set; }
        public string? AiModerationProvider { get; set; }
        public string? AiModerationRiskLevel { get; set; }
        public string? AiModerationCategories { get; set; }
        public string? AiModerationJson { get; set; }
        public DateTimeOffset? AiReviewedAt { get; set; }
        public Guid? ReviewedByAdminId { get; set; }
        public DateTimeOffset? AdminReviewedAt { get; set; }
        public string? AdminNote { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public RoomingHouse RoomingHouse { get; set; } = null!;
        public User TenantUser { get; set; } = null!;
        public User? ReviewedByAdmin { get; set; }
        public RentalContract RentalContract { get; set; } = null!;
        public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    }
}
