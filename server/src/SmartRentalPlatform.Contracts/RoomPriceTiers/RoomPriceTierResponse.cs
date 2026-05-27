using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Contracts.RoomPriceTiers
{
    public class RoomPriceTierResponse
    {
        public Guid Id { get; set; }
        public int OccupantCount { get; set; }
        public decimal MonthlyRent { get; set; }
        public bool IsActive { get; set; }
    }
}
