using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace SmartRentalPlatform.Contracts.RoomingHouseReviews.Requests;

public class CreateRoomingHouseReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<IFormFile> Images { get; set; } = new();
}
