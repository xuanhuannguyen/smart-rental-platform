using System.ComponentModel.DataAnnotations;

namespace SmartRentalPlatform.Contracts.Billing.Requests;

public sealed record UpdateBillingServiceTypeRequest(
    [Required(ErrorMessage = "Tên dịch vụ là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên dịch vụ không được vượt quá 100 ký tự.")]
    string Name,

    bool SupportsMeterReading,

    [StringLength(50, ErrorMessage = "Tên đơn vị đo không được vượt quá 50 ký tự.")]
    string? MeterUnitName
);
