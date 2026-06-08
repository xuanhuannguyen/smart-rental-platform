namespace SmartRentalPlatform.Application.Wallets;

public sealed class WalletTransactionMetadata
{
    public Guid? TransferGroupId { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? Description { get; set; }
}
