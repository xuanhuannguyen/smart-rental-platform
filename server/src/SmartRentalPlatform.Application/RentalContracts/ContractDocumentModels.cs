namespace SmartRentalPlatform.Application.RentalContracts;

public enum ContractDocumentBuildMode
{
    Live = 1,
    ExistingSnapshotOrLive = 2,
    FreezeNewSnapshot = 3
}

public sealed class ContractDocumentModel
{
    public string TemplateVersion { get; init; } = ContractDocumentTemplate.Version;

    public DateTimeOffset PreparedAt { get; init; }

    public string ContractNumber { get; init; } = string.Empty;

    public ContractDocumentParty Landlord { get; init; } = new();

    public ContractDocumentParty Tenant { get; init; } = new();

    public ContractDocumentProperty Property { get; init; } = new();

    public ContractDocumentFinancialTerms FinancialTerms { get; init; } = new();

    public IReadOnlyList<ContractDocumentServicePrice> ServicePrices { get; init; } =
        Array.Empty<ContractDocumentServicePrice>();

    public IReadOnlyList<ContractDocumentOccupant> Occupants { get; init; } =
        Array.Empty<ContractDocumentOccupant>();

    public IReadOnlyList<string> HouseRules { get; init; } = Array.Empty<string>();
}

public sealed class ContractDocumentParty
{
    public Guid UserId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public DateOnly? DateOfBirth { get; init; }

    public string DocumentNumber { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;
}

public sealed class ContractDocumentProperty
{
    public Guid RoomId { get; init; }

    public string RoomNumber { get; init; } = string.Empty;

    public string RoomingHouseName { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public int Floor { get; init; }

    public decimal? AreaM2 { get; init; }

    public int MaxOccupants { get; init; }

    public string Description { get; init; } = string.Empty;
}

public sealed class ContractDocumentFinancialTerms
{
    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public decimal MonthlyRent { get; init; }

    public decimal DepositAmount { get; init; }

    public int PaymentDay { get; init; }

    public DateTimeOffset? DepositPaidAt { get; init; }
}

public sealed class ContractDocumentServicePrice
{
    public string ServiceName { get; init; } = string.Empty;

    public string PricingUnit { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public DateOnly EffectiveFrom { get; init; }

    public string Note { get; init; } = "Chưa bao gồm trong giá thuê";
}

public sealed class ContractDocumentOccupant
{
    public Guid OccupantId { get; init; }

    public Guid? UserId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public DateOnly? DateOfBirth { get; init; }

    public string DocumentNumber { get; init; } = string.Empty;

    public string Relationship { get; init; } = string.Empty;

    public DateOnly MoveInDate { get; init; }

    public DateOnly? MoveOutDate { get; init; }
}

public static class ContractDocumentTemplate
{
    public const string Version = "contract-v2.0";
}
