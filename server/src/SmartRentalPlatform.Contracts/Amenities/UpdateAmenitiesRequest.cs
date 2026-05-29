using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Contracts.Amenities
{
    public class UpdateAmenitiesRequest
    {
        public List<int> AmenityIds { get; set; } = new();
    }
}
