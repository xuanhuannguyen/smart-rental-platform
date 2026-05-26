using SmartRentalPlatform.Domain.Entities.Administrative;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Entities.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Domain.Entities.Properties
{
    public class RoomingHouse
    {
        public Guid Id { get; set; }
        public Guid LandlordUserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string AddressLine { get; set; } = string.Empty;
        public string WardCode { get; set; } = string.Empty;
        public string ProvinceCode { get; set; } = string.Empty;
        public string AddressDisplay { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public RoomingHouseApprovalStatus ApprovalStatus { get; set; } = RoomingHouseApprovalStatus.Pending;
        public RoomingHouseVisibilityStatus VisibilityStatus { get; set; } = RoomingHouseVisibilityStatus.Hidden;
        public string? RejectedReason {  get; set; }
        public Guid? ReviewedByAdminId { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        
        public User Landlord { get; set; } = null!;
        public User? ReviewedByAdmin { get; set; }
        public AdministrativeProvince Province { get; set; } = null!;
        public AdministrativeWard Ward { get; set; } = null!;
        public ICollection<Room> Rooms { get; set; } = new List<Room>();
        public ICollection<RoomingHouseAmenity> RoomingHouseAmenities { get; set; } = new List<RoomingHouseAmenity>();
        public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
        public RoomingHouseLegalDocument LegalDocument { get; set; } = null!;
    }
}
