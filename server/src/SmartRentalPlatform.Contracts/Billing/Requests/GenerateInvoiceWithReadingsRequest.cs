namespace SmartRentalPlatform.Contracts.Billing.Requests;

/// <summary>
/// Request tạo hóa đơn kết hợp nhập chỉ số điện/nước trong cùng một bước.
///
/// MeterReadings: danh sách chỉ số cho các dịch vụ có PricingUnit = MeterReading
/// đang có bảng giá hiệu lực trong kỳ hóa đơn.
///   - Bắt buộc nhập đủ chỉ số cho tất cả dịch vụ MeterReading hiệu lực.
///   - Nếu thiếu chỉ số của một dịch vụ MeterReading, backend trả lỗi.
///   - Dịch vụ PerMonth/PerPersonPerMonth có giá hiệu lực sẽ tự động được thêm vào hóa đơn.
///   - Dịch vụ chưa có giá hiệu lực trong kỳ sẽ không được tính vào hóa đơn.
/// </summary>
public sealed record GenerateInvoiceWithReadingsRequest(
    Guid ContractId,
    DateOnly BillingPeriodStart,
    DateOnly BillingPeriodEnd,
    decimal DiscountAmount,
    string? Note,
    List<MeterReadingInput> MeterReadings);
