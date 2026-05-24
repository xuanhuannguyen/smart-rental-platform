using System.ComponentModel.DataAnnotations;

namespace SmartRentalPlatform.Contracts.Users;

public class UpdateUserProfileRequest
{
    [Required(ErrorMessage = "Họ và tên không được để trống.")]
    [MaxLength(150, ErrorMessage = "Họ và tên không được vượt quá 150 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ngày sinh không được để trống.")]
    public DateOnly DateOfBirth { get; set; }

    [MaxLength(30, ErrorMessage = "Giới tính không được vượt quá 30 ký tự.")]
    public string? Gender { get; set; }

    [Required(ErrorMessage = "Địa chỉ không được để trống.")]
    public string AddressLine { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Tên người liên hệ khẩn cấp không được vượt quá 150 ký tự.")]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20, ErrorMessage = "Số điện thoại khẩn cấp không được vượt quá 20 ký tự.")]
    public string? EmergencyContactPhone { get; set; }
}
