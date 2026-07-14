namespace SmartRentalPlatform.Contracts.PropertyImages.Requests;

public class UpdatePropertyImageItemRequest
{
    public Guid? Id { get; set; }
    public Guid? MediaAssetId { get; set; }
    public string? Caption { get; set; }
    public bool IsCover { get; set; }
    public int SortOrder { get; set; }
}
