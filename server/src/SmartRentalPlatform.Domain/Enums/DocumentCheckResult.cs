namespace SmartRentalPlatform.Domain.Enums;

public enum DocumentCheckResult
{
    Unknown = 0,
    Valid = 1,
    Blurry = 2,
    MissingCorner = 3,
    Expired = 4,
    Tampered = 5,
    WrongDocumentType = 6,
    Unreadable = 7
}
