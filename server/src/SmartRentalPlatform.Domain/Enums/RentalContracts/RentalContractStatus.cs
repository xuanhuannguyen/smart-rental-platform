namespace SmartRentalPlatform.Domain.Enums.RentalContracts
{
    public enum RentalContractStatus
    {
        WaitingTenantOccupants = 1,
        PendingLandlordSignature = 2,
        LandlordRevisionRequested = 3,
        PendingTenantSignature = 4,
        TenantRevisionRequested = 5,
        Rejected = 6,
        Cancelled = 7,
        Expired = 8,
        Active = 9
    }
}
