using System.ComponentModel.DataAnnotations;

namespace SmartRentalPlatform.Contracts.Amenities.Requests;

public sealed record CreateAmenityRequest(
    [Required(ErrorMessage = "Tên tiện ích là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên tiện ích không được vượt quá 100 ký tự.")]
    string Name,

    [Required(ErrorMessage = "Phạm vi tiện ích là bắt buộc.")]
    string Scope,

    [Required(ErrorMessage = "Icon tiện ích là bắt buộc.")]
    [StringLength(50, ErrorMessage = "Icon tiện ích không được vượt quá 50 ký tự.")]
    string IconCode
);
