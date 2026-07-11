namespace SmartRentalPlatform.Contracts.Billing.Requests;

/// <summary>
/// Chỉ số đồng hồ được nhập inline khi tạo hóa đơn.
/// Chỉ áp dụng cho dịch vụ có PricingUnit = MeterReading.
/// </summary>
public sealed record MeterReadingInput(
    Guid ServiceTypeId,
    decimal? PreviousReading,
    decimal CurrentReading,
    string? ProofImageObjectKey,
    Guid? ProofMediaAssetId = null);
