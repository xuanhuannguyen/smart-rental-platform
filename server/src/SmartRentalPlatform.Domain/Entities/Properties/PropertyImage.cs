using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Domain.Entities.Properties
{
    public class PropertyImage
    {
        public Guid Id { get; set; }
        public Guid? RoomingHouseId { get; set; }
        public Guid? RoomId { get; set; }
        public Guid? RoomingHouseReviewId { get; set; }
        public string ObjectKey { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public bool IsCover { get; set; } = false;
        public int SortOrder { get; set; } = 0;
        public DateTimeOffset CreatedAt { get; set; }

        
        public RoomingHouse? RoomingHouse { get; set; }
        public Room? Room { get; set; }
        public RoomingHouseReview? RoomingHouseReview { get; set; }
    }
}
