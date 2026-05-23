using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Domain.Entities.Administrative
{
    public class AdministrativeProvince
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ProvinceType Type { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public ICollection<AdministrativeDistrict> Districts { get; set; } = new List<AdministrativeDistrict>();
        public ICollection<RoomingHouse> RoomingHouses { get; set; } = new List<RoomingHouse>();
    }
}
