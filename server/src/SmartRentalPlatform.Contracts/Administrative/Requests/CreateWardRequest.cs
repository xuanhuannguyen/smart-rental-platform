using System.ComponentModel.DataAnnotations;

namespace SmartRentalPlatform.Contracts.Administrative.Requests;

public sealed record CreateWardRequest(
    [Required(ErrorMessage = "Mã phường/xã là bắt buộc.")]
    [StringLength(20, ErrorMessage = "Mã phường/xã không được vượt quá 20 ký tự.")]
    string Code,

    [Required(ErrorMessage = "Mã tỉnh/thành phố là bắt buộc.")]
    string ProvinceCode,

    [Required(ErrorMessage = "Tên phường/xã là bắt buộc.")]
    [StringLength(255, ErrorMessage = "Tên phường/xã không được vượt quá 255 ký tự.")]
    string Name,

    [Required(ErrorMessage = "Loại phường/xã là bắt buộc.")]
    string Type
);
