using System.ComponentModel.DataAnnotations;

namespace SmartRentalPlatform.Contracts.Administrative.Requests;

public sealed record UpdateProvinceRequest(
    [Required(ErrorMessage = "Tên tỉnh/thành phố là bắt buộc.")]
    [StringLength(255, ErrorMessage = "Tên tỉnh/thành phố không được vượt quá 255 ký tự.")]
    string Name,

    [Required(ErrorMessage = "Loại tỉnh/thành phố là bắt buộc.")]
    string Type
);
