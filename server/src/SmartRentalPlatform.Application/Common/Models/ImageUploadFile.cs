namespace SmartRentalPlatform.Application.Common.Models;

public class ImageUploadFile
{
    public Stream Content { get; set; } = Stream.Null;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Length { get; set; }
}
