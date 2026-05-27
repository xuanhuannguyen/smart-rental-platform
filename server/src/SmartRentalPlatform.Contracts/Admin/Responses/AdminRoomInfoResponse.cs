using System;

namespace SmartRentalPlatform.Contracts.Admin;

public class AdminRoomInfoResponse
{
    public Guid Id { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int Floor { get; set; }
    public decimal? AreaM2 { get; set; }
    public int MaxOccupants { get; set; }
    public string Status { get; set; } = string.Empty;
}
