namespace SmartRentalPlatform.Infrastructure.MediaMigration;

public sealed class LegacyMediaMigrationReadinessReport
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public bool DryRun { get; init; } = true;

    public bool StorageCheckRequested { get; init; }

    public int SampleLimitPerModule { get; init; }

    public List<LegacyMediaModuleReport> Modules { get; init; } = new();

    public LegacyMediaMigrationTotals Totals { get; init; } = new();
}

public sealed class LegacyMediaMigrationTotals
{
    public int LegacyReferences { get; init; }

    public int MissingMediaAssetLinks { get; init; }

    public int ExistingMediaAssetLinks { get; init; }

    public int MatchingMediaAssetsByObjectKey { get; init; }

    public int MissingMediaAssetsByObjectKey { get; init; }

    public int StoragePresent { get; init; }

    public int StorageMissing { get; init; }

    public int StorageErrors { get; init; }
}

public sealed class LegacyMediaModuleReport
{
    public string Module { get; init; } = string.Empty;

    public int LegacyReferences { get; init; }

    public int MissingMediaAssetLinks { get; init; }

    public int ExistingMediaAssetLinks { get; init; }

    public int MatchingMediaAssetsByObjectKey { get; init; }

    public int MissingMediaAssetsByObjectKey { get; init; }

    public bool StorageChecked { get; init; }

    public int StoragePresent { get; init; }

    public int StorageMissing { get; init; }

    public int StorageErrors { get; init; }

    public List<LegacyMediaReferenceSample> Samples { get; init; } = new();
}

public sealed class LegacyMediaReferenceSample
{
    public string Module { get; init; } = string.Empty;

    public string EntityName { get; init; } = string.Empty;

    public string EntityId { get; init; } = string.Empty;

    public string FieldName { get; init; } = string.Empty;

    public string ObjectKey { get; init; } = string.Empty;

    public Guid? ExistingMediaAssetId { get; init; }

    public Guid? MatchingMediaAssetId { get; init; }

    public string StorageStatus { get; init; } = "NotChecked";

    public string? Note { get; init; }
}

public sealed class LegacyMediaMigrationReadinessOptions
{
    public bool CheckStorage { get; init; }

    public int SampleLimitPerModule { get; init; } = 10;
}

public sealed class LegacyMediaBackfillOptions
{
    public bool DryRun { get; init; } = true;

    public bool CheckStorage { get; init; }

    public bool RequireStoragePresent { get; init; }

    public int SampleLimitPerModule { get; init; } = 10;
}

public sealed class LegacyMediaBackfillReport
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public bool DryRun { get; init; } = true;

    public bool StorageCheckRequested { get; init; }

    public bool RequireStoragePresent { get; init; }

    public List<LegacyMediaBackfillModuleReport> Modules { get; init; } = new();

    public LegacyMediaBackfillTotals Totals { get; init; } = new();
}

public sealed class LegacyMediaBackfillTotals
{
    public int Candidates { get; init; }

    public int PlannedCreates { get; init; }

    public int PlannedLinks { get; init; }

    public int CreatedMediaAssets { get; init; }

    public int LinkedLegacyRows { get; init; }

    public int SkippedSchemaNotReady { get; init; }

    public int SkippedStorageMissing { get; init; }

    public int SkippedAlreadyLinked { get; init; }
}

public sealed class LegacyMediaBackfillModuleReport
{
    public string Module { get; init; } = string.Empty;

    public int Candidates { get; init; }

    public int PlannedCreates { get; init; }

    public int PlannedLinks { get; init; }

    public int CreatedMediaAssets { get; init; }

    public int LinkedLegacyRows { get; init; }

    public int SkippedSchemaNotReady { get; init; }

    public int SkippedStorageMissing { get; init; }

    public int SkippedAlreadyLinked { get; init; }

    public List<LegacyMediaBackfillSample> Samples { get; init; } = new();
}

public sealed class LegacyMediaBackfillSample
{
    public string Module { get; init; } = string.Empty;

    public string EntityName { get; init; } = string.Empty;

    public string EntityId { get; init; } = string.Empty;

    public string FieldName { get; init; } = string.Empty;

    public string ObjectKey { get; init; } = string.Empty;

    public Guid? ExistingMediaAssetId { get; init; }

    public Guid? TargetMediaAssetId { get; init; }

    public string Action { get; init; } = string.Empty;

    public string? Reason { get; init; }
}

public sealed class LegacyMediaCleanupOptions
{
    public bool DryRun { get; init; } = true;

    public int SampleLimitPerModule { get; init; } = 10;
}

public sealed class LegacyMediaCleanupReport
{
    public DateTimeOffset GeneratedAtUtc { get; init; }

    public bool DryRun { get; init; } = true;

    public bool StorageCheckRequested { get; init; } = true;

    public List<LegacyMediaCleanupModuleReport> Modules { get; init; } = new();

    public LegacyMediaCleanupTotals Totals { get; init; } = new();
}

public sealed class LegacyMediaCleanupTotals
{
    public int Candidates { get; init; }

    public int PlannedDeletes { get; init; }

    public int PlannedClears { get; init; }

    public int AppliedDeletes { get; init; }

    public int AppliedClears { get; init; }

    public int SkippedStoragePresent { get; init; }

    public int SkippedStorageError { get; init; }

    public int SkippedNoCleanupTarget { get; init; }
}

public sealed class LegacyMediaCleanupModuleReport
{
    public string Module { get; init; } = string.Empty;

    public int Candidates { get; init; }

    public int PlannedDeletes { get; init; }

    public int PlannedClears { get; init; }

    public int AppliedDeletes { get; init; }

    public int AppliedClears { get; init; }

    public int SkippedStoragePresent { get; init; }

    public int SkippedStorageError { get; init; }

    public int SkippedNoCleanupTarget { get; init; }

    public List<LegacyMediaCleanupSample> Samples { get; init; } = new();
}

public sealed class LegacyMediaCleanupSample
{
    public string Module { get; init; } = string.Empty;

    public string EntityName { get; init; } = string.Empty;

    public string EntityId { get; init; } = string.Empty;

    public string FieldName { get; init; } = string.Empty;

    public string ObjectKey { get; init; } = string.Empty;

    public Guid? ExistingMediaAssetId { get; init; }

    public string Action { get; init; } = string.Empty;

    public string? Reason { get; init; }
}
