namespace SmartRentalPlatform.Contracts.Billing.Responses;

public sealed record BulkInvoiceResultResponse(
    int TotalActiveRooms,
    int CreatedCount,
    int SkippedCount,
    int MissingDataCount,
    IReadOnlyCollection<BulkInvoiceRoomResultResponse> Rooms);

public sealed record BulkInvoiceRoomResultResponse(
    Guid RoomId,
    Guid ContractId,
    string RoomNumber,
    string Status,
    string Message,
    InvoiceResponse? Invoice);
