namespace SmartRentalPlatform.Contracts.Kyc;

public class KycHistoryResponse
{
    public IReadOnlyList<KycHistoryItemResponse> Items { get; set; } = [];
    public int TotalItems { get; set; }
}
