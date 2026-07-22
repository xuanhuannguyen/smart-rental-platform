namespace SmartRentalPlatform.Domain.Enums.RentalContracts;

public enum SigningEnvelopeStatus
{
    Draft = 1,
    SentToProvider = 2,
    WaitingForSigners = 3,
    PartiallySigned = 4,
    Completed = 5,
    Failed = 6,
    Expired = 7,
    Cancelled = 8
}
