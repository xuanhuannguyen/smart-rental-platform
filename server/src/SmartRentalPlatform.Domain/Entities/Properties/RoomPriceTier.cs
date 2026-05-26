using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Domain.Entities.Properties
{
    public class RoomPriceTier
    {
        public Guid Id { get; set; }
        public Guid RoomId { get; set; }
        public int OccupantCount { get; set; }
        public decimal MonthlyRent { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }


        public Room Room { get; set; } = null!;
    }
}
