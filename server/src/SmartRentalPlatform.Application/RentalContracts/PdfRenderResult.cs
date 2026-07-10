namespace SmartRentalPlatform.Application.RentalContracts;

/// <summary>
/// Result of a PDF render operation that includes both the raw PDF bytes and the
/// signature zone coordinates extracted from the layout engine.
/// </summary>
public sealed class PdfRenderResult
{
    /// <summary>Raw PDF bytes ready to be stored or uploaded.</summary>
    public required byte[] PdfBytes { get; init; }

    /// <summary>
    /// Signature zone positions keyed by signer role ("Landlord", "Tenant").
    /// Coordinates are in PDF points using a top-left origin, matching VNPT's coordinate system.
    /// May be empty when rendering non-ESign variants (preview, masked reference, etc.).
    /// </summary>
    public IReadOnlyDictionary<string, SignatureZone> SignatureZones { get; init; }
        = new Dictionary<string, SignatureZone>();
}

/// <summary>
/// Bounding box of a signature field inside a PDF page.
/// Origin is top-left, unit is PDF points (1 pt = 1/72 inch), matching VNPT eContract's coordinate system.
/// </summary>
public sealed class SignatureZone
{
    /// <summary>Left edge of the signature box (points from left of page).</summary>
    public int X { get; init; }

    /// <summary>Top edge of the signature box (points from top of page).</summary>
    public int Y { get; init; }

    /// <summary>Width of the signature box in points.</summary>
    public int Width { get; init; }

    /// <summary>Height of the signature box in points.</summary>
    public int Height { get; init; }

    /// <summary>1-indexed page number where the signature box appears.</summary>
    public int Page { get; init; }
}
