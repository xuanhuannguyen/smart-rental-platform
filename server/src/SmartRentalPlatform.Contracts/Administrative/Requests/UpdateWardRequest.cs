using System.ComponentModel.DataAnnotations;

namespace SmartRentalPlatform.Contracts.Administrative.Requests;

public sealed record UpdateWardRequest(
    [Required(ErrorMessage = "Tên phường/xã là bắt buộc.")]
    [StringLength(255, ErrorMessage = "Tên phường/xã không được vượt quá 255 ký tự.")]
    string Name,

    [Required(ErrorMessage = "Loại phường/xã là bắt buộc.")]
    string Type
);
