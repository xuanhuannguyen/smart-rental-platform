using System;
using System.Collections.Generic;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.LegalDocuments;
using SmartRentalPlatform.Contracts.PropertyImages;

namespace SmartRentalPlatform.Contracts.Admin.Responses;

public class AdminRoomingHouseDetailResponse : AdminRoomingHouseListItemResponse
{
    public string? Description { get; set; }
    public string AddressLine { get; set; } = string.Empty;
    public string ProvinceCode { get; set; } = string.Empty;
    public string WardCode { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? RejectedReason { get; set; }
    public Guid? ReviewedByAdminId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public RoomingHouseLegalDocumentResponse? LegalDocument { get; set; }
    public List<PropertyImageResponse> Images { get; set; } = new();
    public List<AmenityResponse> Amenities { get; set; } = new();
    public List<AdminRoomInfoResponse> Rooms { get; set; } = new();
}
