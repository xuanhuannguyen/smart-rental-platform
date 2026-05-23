using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Domain.Entities.Administrative
{
    public class AdministrativeDistrict
    {
        public string Code { get; set; } = string.Empty;
        public string ProvinceCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DistrictType Type { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public AdministrativeProvince Province { get; set; } = null!;
        public ICollection<AdministrativeWard> Wards { get; set; } = new List<AdministrativeWard>();
        public ICollection<RoomingHouse> RoomingHouses { get; set; } = new List<RoomingHouse>();
    }
}
