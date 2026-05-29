using System;

namespace SmartRentalPlatform.Contracts.Admin.Responses;

public class AdminRoomingHouseListItemResponse
{
    public Guid Id { get; set; }
    public Guid LandlordUserId { get; set; }
    public string LandlordEmail { get; set; } = string.Empty;
    public string LandlordName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AddressDisplay { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public string VisibilityStatus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
