namespace SmartRentalPlatform.Contracts.PropertyImages.Requests;

public class UpdatePropertyImagesRequest
{
    public List<UpdatePropertyImageItemRequest> Images { get; set; } = [];
}
