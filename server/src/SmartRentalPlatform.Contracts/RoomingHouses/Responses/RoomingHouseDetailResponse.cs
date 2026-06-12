using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.LegalDocuments;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RentalPolicies.Responses;

namespace SmartRentalPlatform.Contracts.RoomingHouses.Responses;

public class RoomingHouseDetailResponse
{
    public Guid Id { get; set; }

    public Guid LandlordUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string AddressLine { get; set; } = string.Empty;

    public string ProvinceCode { get; set; } = string.Empty;

    public string WardCode { get; set; } = string.Empty;

    public string AddressDisplay { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string ApprovalStatus { get; set; } = string.Empty;

    public string VisibilityStatus { get; set; } = string.Empty;

    public string? RejectedReason { get; set; }

    public Guid? ReviewedByAdminId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public RoomingHouseLegalDocumentResponse? LegalDocument { get; set; }

    public RentalPolicyResponse? RentalPolicy { get; set; }

    public List<PropertyImageResponse> Images { get; set; } = new();

    public List<AmenityResponse> Amenities { get; set; } = new();
}
