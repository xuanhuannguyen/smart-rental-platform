using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
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
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public RoomingHouse RoomingHouse { get; set; } = null!;
        public User TenantUser { get; set; } = null!;
        public RentalContract RentalContract { get; set; } = null!;
        public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    }
}
