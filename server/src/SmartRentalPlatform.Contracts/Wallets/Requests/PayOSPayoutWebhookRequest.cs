using System.Text.Json.Serialization;

namespace SmartRentalPlatform.Contracts.Wallets.Requests;

public sealed record PayOSPayoutWebhookRequest
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public PayOSPayoutWebhookData Data { get; init; } = new();

    [JsonPropertyName("signature")]
    public string? Signature { get; init; }
}

public sealed record PayOSPayoutWebhookData
{
    [JsonPropertyName("payouts")]
    public List<PayOSPayoutWebhookPayout> Payouts { get; init; } = new();
}

public sealed record PayOSPayoutWebhookPayout
{
    [JsonPropertyName("referenceId")]
    public string ReferenceId { get; init; } = string.Empty;
    
    [JsonPropertyName("approvalState")]
    public string ApprovalState { get; init; } = string.Empty;
    
    [JsonPropertyName("transactions")]
    public List<PayOSPayoutWebhookTransaction> Transactions { get; init; } = new();
}

public sealed record PayOSPayoutWebhookTransaction
{
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;
}
