using System.ComponentModel.DataAnnotations;

namespace SmartRentalPlatform.Contracts.Users.Requests;

public class UpdateUserProfileRequest
{
    [Required(ErrorMessage = "Tên hiển thị không được để trống.")]
    [MaxLength(100, ErrorMessage = "Tên hiển thị không được vượt quá 100 ký tự.")]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự.")]
    public string? PhoneNumber { get; set; }

    [MaxLength(150, ErrorMessage = "Tên người liên hệ khẩn cấp không được vượt quá 150 ký tự.")]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20, ErrorMessage = "Số điện thoại khẩn cấp không được vượt quá 20 ký tự.")]
    public string? EmergencyContactPhone { get; set; }

    public string? AvatarUrl { get; set; }

    public Guid? AvatarMediaAssetId { get; set; }
}
