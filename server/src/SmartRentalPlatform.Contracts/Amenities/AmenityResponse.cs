using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Contracts.Amenities
{
    public class AmenityResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string? IconCode { get; set; }
    }
}
