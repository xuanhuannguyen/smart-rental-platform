namespace SmartRentalPlatform.Domain.Enums.RentalContracts
{
    public enum ContractAppendixStatus
    {
        Draft = 1,
        PendingSignature = 2,
        Active = 3,
        Rejected = 4,
        Cancelled = 5,
        LandlordRevisionRequested = 6,
        TenantRevisionRequested = 7
    }
}
