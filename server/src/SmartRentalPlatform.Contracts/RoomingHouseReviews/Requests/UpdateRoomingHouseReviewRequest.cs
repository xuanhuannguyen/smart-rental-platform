using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace SmartRentalPlatform.Contracts.RoomingHouseReviews.Requests;

public class UpdateRoomingHouseReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<Guid> RetainedImageIds { get; set; } = new();
    public List<IFormFile> NewImages { get; set; } = new();
}
