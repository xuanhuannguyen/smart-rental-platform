using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Interfaces.Media;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Enums.Media;
using System.Data;

namespace SmartRentalPlatform.Infrastructure.MediaMigration;

public sealed class LegacyMediaMigrationReadinessService
{
    private readonly IAppDbContext _dbContext;
    private readonly DbContext? _efDbContext;
    private readonly IMediaStorageService? _mediaStorageService;

    public LegacyMediaMigrationReadinessService(
        IAppDbContext dbContext,
        IMediaStorageService? mediaStorageService = null)
    {
        _dbContext = dbContext;
        _efDbContext = dbContext as DbContext;
        _mediaStorageService = mediaStorageService;
    }

    public async Task<LegacyMediaMigrationReadinessReport> BuildReportAsync(
        LegacyMediaMigrationReadinessOptions options,
        CancellationToken cancellationToken = default)
    {
        var references = await LoadLegacyReferencesAsync(cancellationToken);
        var normalizedKeys = references
            .Select(x => x.ObjectKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matchingMediaAssets = await CanQueryTableAsync("media_assets", cancellationToken) &&
                                  await CanQueryColumnAsync("media_assets", "object_key", cancellationToken)
            ? await _dbContext.MediaAssets
                .AsNoTracking()
                .Where(x => normalizedKeys.Contains(x.ObjectKey))
                .Select(x => new { x.Id, x.ObjectKey })
                .ToListAsync(cancellationToken)
            : [];

        var mediaAssetByObjectKey = matchingMediaAssets
            .GroupBy(x => x.ObjectKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Id, StringComparer.OrdinalIgnoreCase);

        var enrichedReferences = new List<EnrichedLegacyMediaReference>(references.Count);
        foreach (var reference in references)
        {
            mediaAssetByObjectKey.TryGetValue(reference.ObjectKey, out var matchingMediaAssetId);

            var storageStatus = "NotChecked";
            string? note = null;

            if (options.CheckStorage)
            {
                if (_mediaStorageService is null)
                {
                    storageStatus = "Skipped";
                    note = "Storage check requested but no media storage service was provided.";
                }
                else
                {
                    try
                    {
                        storageStatus = await _mediaStorageService.ExistsAsync(reference.ObjectKey, cancellationToken)
                            ? "Present"
                            : "Missing";
                    }
                    catch (Exception ex)
                    {
                        storageStatus = "Error";
                        note = ex.Message;
                    }
                }
            }

            enrichedReferences.Add(new EnrichedLegacyMediaReference(reference, matchingMediaAssetId == Guid.Empty ? null : matchingMediaAssetId, storageStatus, note));
        }

        var modules = enrichedReferences
            .GroupBy(x => x.Reference.Module)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildModuleReport(group, options))
            .ToList();

        var totals = new LegacyMediaMigrationTotals
        {
            LegacyReferences = modules.Sum(x => x.LegacyReferences),
            MissingMediaAssetLinks = modules.Sum(x => x.MissingMediaAssetLinks),
            ExistingMediaAssetLinks = modules.Sum(x => x.ExistingMediaAssetLinks),
            MatchingMediaAssetsByObjectKey = modules.Sum(x => x.MatchingMediaAssetsByObjectKey),
            MissingMediaAssetsByObjectKey = modules.Sum(x => x.MissingMediaAssetsByObjectKey),
            StoragePresent = modules.Sum(x => x.StoragePresent),
            StorageMissing = modules.Sum(x => x.StorageMissing),
            StorageErrors = modules.Sum(x => x.StorageErrors)
        };

        return new LegacyMediaMigrationReadinessReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            DryRun = true,
            StorageCheckRequested = options.CheckStorage,
            SampleLimitPerModule = options.SampleLimitPerModule,
            Modules = modules,
            Totals = totals
        };
    }

    public async Task<LegacyMediaBackfillReport> BackfillAsync(
        LegacyMediaBackfillOptions options,
        CancellationToken cancellationToken = default)
    {
        var references = await LoadLegacyReferencesAsync(cancellationToken);
        var normalizedKeys = references
            .Select(x => x.ObjectKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var canUseMediaAssets =
            await CanQueryTableAsync("media_assets", cancellationToken) &&
            await CanQueryColumnAsync("media_assets", "object_key", cancellationToken);

        var existingMediaAssetsByObjectKey = new Dictionary<string, MediaAsset>(StringComparer.OrdinalIgnoreCase);
        if (canUseMediaAssets)
        {
            existingMediaAssetsByObjectKey = (await _dbContext.MediaAssets
                .Where(x => normalizedKeys.Contains(x.ObjectKey))
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.ObjectKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        }

        var actions = new List<LegacyMediaBackfillAction>();
        foreach (var reference in references)
        {
            var target = ResolveBackfillTarget(reference);
            if (target is null)
            {
                actions.Add(LegacyMediaBackfillAction.Skip(reference, "SkippedSchemaNotReady", "No backfill target is defined for this legacy reference."));
                continue;
            }

            var targetTableExists = await CanQueryTableAsync(target.TableName, cancellationToken);
            var targetLinkColumnExists = targetTableExists &&
                                         await CanQueryColumnAsync(target.TableName, target.LinkColumnName, cancellationToken);
            if (!targetTableExists || !targetLinkColumnExists)
            {
                actions.Add(LegacyMediaBackfillAction.Skip(
                    reference,
                    "SkippedSchemaNotReady",
                    $"Target schema missing: table '{target.TableName}' exists={targetTableExists}, link column '{target.LinkColumnName}' exists={targetLinkColumnExists}."));
                continue;
            }

            if (reference.ExistingMediaAssetId.HasValue)
            {
                actions.Add(LegacyMediaBackfillAction.Skip(reference, "SkippedAlreadyLinked", "Legacy row already has a MediaAssetId."));
                continue;
            }

            if (options.RequireStoragePresent)
            {
                var storagePresent = options.CheckStorage && _mediaStorageService is not null &&
                                     await _mediaStorageService.ExistsAsync(reference.ObjectKey, cancellationToken);
                if (!storagePresent)
                {
                    actions.Add(LegacyMediaBackfillAction.Skip(reference, "SkippedStorageMissing", "Storage object is missing or was not checked."));
                    continue;
                }
            }

            existingMediaAssetsByObjectKey.TryGetValue(reference.ObjectKey, out var existingMediaAsset);
            if (existingMediaAsset is not null)
            {
                actions.Add(LegacyMediaBackfillAction.Link(reference, target, existingMediaAsset.Id, createMediaAsset: false));
                continue;
            }

            if (!canUseMediaAssets)
            {
                actions.Add(LegacyMediaBackfillAction.Skip(reference, "SkippedSchemaNotReady", "media_assets table or object_key column is missing."));
                continue;
            }

            var mediaAsset = CreateLegacyMediaAsset(reference, target);
            existingMediaAssetsByObjectKey[reference.ObjectKey] = mediaAsset;
            actions.Add(LegacyMediaBackfillAction.Link(reference, target, mediaAsset.Id, createMediaAsset: true, mediaAsset));
        }

        if (!options.DryRun)
        {
            await ApplyBackfillActionsAsync(actions, cancellationToken);
        }

        return BuildBackfillReport(actions, options);
    }

    public async Task<LegacyMediaCleanupReport> CleanupMissingStorageAsync(
        LegacyMediaCleanupOptions options,
        CancellationToken cancellationToken = default)
    {
        if (_mediaStorageService is null)
        {
            throw new InvalidOperationException("Phase 5E cleanup requires a configured media storage service.");
        }

        var references = await LoadLegacyReferencesAsync(cancellationToken);
        var actions = new List<LegacyMediaCleanupAction>(references.Count);

        foreach (var reference in references)
        {
            bool exists;
            try
            {
                exists = await _mediaStorageService.ExistsAsync(reference.ObjectKey, cancellationToken);
            }
            catch (Exception ex)
            {
                actions.Add(LegacyMediaCleanupAction.Skip(
                    reference,
                    "SkippedStorageError",
                    ex.Message));
                continue;
            }

            if (exists)
            {
                actions.Add(LegacyMediaCleanupAction.Skip(
                    reference,
                    "SkippedStoragePresent",
                    "Storage object exists; keep the reference."));
                continue;
            }

            var target = ResolveCleanupTarget(reference);
            actions.Add(target is null
                ? LegacyMediaCleanupAction.Skip(reference, "SkippedNoCleanupTarget", "No cleanup target is defined for this legacy reference.")
                : LegacyMediaCleanupAction.Cleanup(reference, target));
        }

        if (!options.DryRun)
        {
            await ApplyCleanupActionsAsync(actions, cancellationToken);
        }

        return BuildCleanupReport(actions, options);
    }

    private async Task ApplyBackfillActionsAsync(
        IReadOnlyCollection<LegacyMediaBackfillAction> actions,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var mediaAssetsToCreate = actions
                .Where(x => x.CreateMediaAsset && x.MediaAsset is not null)
                .Select(x => x.MediaAsset!)
                .GroupBy(x => x.ObjectKey, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            if (mediaAssetsToCreate.Count > 0)
            {
                _dbContext.MediaAssets.AddRange(mediaAssetsToCreate);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            foreach (var action in actions.Where(x => x.Action == "Link"))
            {
                await UpdateLegacyLinkAsync(action, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ApplyCleanupActionsAsync(
        IReadOnlyCollection<LegacyMediaCleanupAction> actions,
        CancellationToken cancellationToken)
    {
        if (_efDbContext is null)
        {
            return;
        }

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var cleanupTargets = actions
                .Where(x => x.Target is not null)
                .Select(x => x.Target!)
                .DistinctBy(x => x.OperationKey)
                .ToList();

            foreach (var target in cleanupTargets)
            {
                await ApplyCleanupTargetAsync(target, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ApplyCleanupTargetAsync(
        LegacyMediaCleanupTarget target,
        CancellationToken cancellationToken)
    {
        if (_efDbContext is null)
        {
            return;
        }

        var connection = _efDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            if (_efDbContext.Database.CurrentTransaction is { } currentTransaction)
            {
                command.Transaction = currentTransaction.GetDbTransaction();
            }

            if (target.DeleteRow)
            {
                command.CommandText = $"""
                    delete from {QuoteIdentifier(target.TableName)}
                    where {QuoteIdentifier(target.PrimaryKeyColumnName)} = @entity_id
                    """;
            }
            else
            {
                var assignments = new List<string>
                {
                    $"{QuoteIdentifier(target.ObjectKeyColumnName)} = {(target.ClearObjectKeyToNull ? "null" : "''")}"
                };

                if (!string.IsNullOrWhiteSpace(target.LinkColumnName))
                {
                    assignments.Add($"{QuoteIdentifier(target.LinkColumnName)} = null");
                }

                command.CommandText = $"""
                    update {QuoteIdentifier(target.TableName)}
                    set {string.Join(", ", assignments)}
                    where {QuoteIdentifier(target.PrimaryKeyColumnName)} = @entity_id
                    """;
            }

            var entityIdParameter = command.CreateParameter();
            entityIdParameter.ParameterName = "entity_id";
            entityIdParameter.Value = Guid.Parse(target.EntityId);
            command.Parameters.Add(entityIdParameter);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task UpdateLegacyLinkAsync(
        LegacyMediaBackfillAction action,
        CancellationToken cancellationToken)
    {
        if (_efDbContext is null || action.Target is null || !action.TargetMediaAssetId.HasValue)
        {
            return;
        }

        var connection = _efDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            if (_efDbContext.Database.CurrentTransaction is { } currentTransaction)
            {
                command.Transaction = currentTransaction.GetDbTransaction();
            }

            command.CommandText = $"""
                update {QuoteIdentifier(action.Target.TableName)}
                set {QuoteIdentifier(action.Target.LinkColumnName)} = @media_asset_id
                where {QuoteIdentifier(action.Target.PrimaryKeyColumnName)} = @entity_id
                  and {QuoteIdentifier(action.Target.LinkColumnName)} is null
                """;

            var mediaAssetIdParameter = command.CreateParameter();
            mediaAssetIdParameter.ParameterName = "media_asset_id";
            mediaAssetIdParameter.Value = action.TargetMediaAssetId.Value;
            command.Parameters.Add(mediaAssetIdParameter);

            var entityIdParameter = command.CreateParameter();
            entityIdParameter.ParameterName = "entity_id";
            entityIdParameter.Value = Guid.Parse(action.Reference.EntityId);
            command.Parameters.Add(entityIdParameter);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private LegacyMediaBackfillReport BuildBackfillReport(
        IReadOnlyCollection<LegacyMediaBackfillAction> actions,
        LegacyMediaBackfillOptions options)
    {
        var modules = actions
            .GroupBy(x => x.Reference.Module)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var moduleActions = group.ToList();
                return new LegacyMediaBackfillModuleReport
                {
                    Module = group.Key,
                    Candidates = moduleActions.Count,
                    PlannedCreates = moduleActions.Count(x => x.CreateMediaAsset),
                    PlannedLinks = moduleActions.Count(x => x.Action == "Link"),
                    CreatedMediaAssets = options.DryRun ? 0 : moduleActions.Count(x => x.CreateMediaAsset),
                    LinkedLegacyRows = options.DryRun ? 0 : moduleActions.Count(x => x.Action == "Link"),
                    SkippedSchemaNotReady = moduleActions.Count(x => x.Action == "SkippedSchemaNotReady"),
                    SkippedStorageMissing = moduleActions.Count(x => x.Action == "SkippedStorageMissing"),
                    SkippedAlreadyLinked = moduleActions.Count(x => x.Action == "SkippedAlreadyLinked"),
                    Samples = moduleActions
                        .Where(x => x.Action != "Link" || x.CreateMediaAsset)
                        .Take(options.SampleLimitPerModule)
                        .Select(x => new LegacyMediaBackfillSample
                        {
                            Module = x.Reference.Module,
                            EntityName = x.Reference.EntityName,
                            EntityId = x.Reference.EntityId,
                            FieldName = x.Reference.FieldName,
                            ObjectKey = x.Reference.ObjectKey,
                            ExistingMediaAssetId = x.Reference.ExistingMediaAssetId,
                            TargetMediaAssetId = x.TargetMediaAssetId,
                            Action = x.Action,
                            Reason = x.Reason
                        })
                        .ToList()
                };
            })
            .ToList();

        return new LegacyMediaBackfillReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            DryRun = options.DryRun,
            StorageCheckRequested = options.CheckStorage,
            RequireStoragePresent = options.RequireStoragePresent,
            Modules = modules,
            Totals = new LegacyMediaBackfillTotals
            {
                Candidates = modules.Sum(x => x.Candidates),
                PlannedCreates = modules.Sum(x => x.PlannedCreates),
                PlannedLinks = modules.Sum(x => x.PlannedLinks),
                CreatedMediaAssets = modules.Sum(x => x.CreatedMediaAssets),
                LinkedLegacyRows = modules.Sum(x => x.LinkedLegacyRows),
                SkippedSchemaNotReady = modules.Sum(x => x.SkippedSchemaNotReady),
                SkippedStorageMissing = modules.Sum(x => x.SkippedStorageMissing),
                SkippedAlreadyLinked = modules.Sum(x => x.SkippedAlreadyLinked)
            }
        };
    }

    private static LegacyMediaCleanupReport BuildCleanupReport(
        IReadOnlyCollection<LegacyMediaCleanupAction> actions,
        LegacyMediaCleanupOptions options)
    {
        var modules = actions
            .GroupBy(x => x.Reference.Module)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var moduleActions = group.ToList();
                var uniqueTargets = moduleActions
                    .Where(x => x.Target is not null)
                    .Select(x => x.Target!)
                    .DistinctBy(x => x.OperationKey)
                    .ToList();

                return new LegacyMediaCleanupModuleReport
                {
                    Module = group.Key,
                    Candidates = moduleActions.Count,
                    PlannedDeletes = uniqueTargets.Count(x => x.DeleteRow),
                    PlannedClears = uniqueTargets.Count(x => !x.DeleteRow),
                    AppliedDeletes = options.DryRun ? 0 : uniqueTargets.Count(x => x.DeleteRow),
                    AppliedClears = options.DryRun ? 0 : uniqueTargets.Count(x => !x.DeleteRow),
                    SkippedStoragePresent = moduleActions.Count(x => x.Action == "SkippedStoragePresent"),
                    SkippedStorageError = moduleActions.Count(x => x.Action == "SkippedStorageError"),
                    SkippedNoCleanupTarget = moduleActions.Count(x => x.Action == "SkippedNoCleanupTarget"),
                    Samples = moduleActions
                        .Where(x => x.Action != "SkippedStoragePresent")
                        .Take(options.SampleLimitPerModule)
                        .Select(x => new LegacyMediaCleanupSample
                        {
                            Module = x.Reference.Module,
                            EntityName = x.Reference.EntityName,
                            EntityId = x.Reference.EntityId,
                            FieldName = x.Reference.FieldName,
                            ObjectKey = x.Reference.ObjectKey,
                            ExistingMediaAssetId = x.Reference.ExistingMediaAssetId,
                            Action = x.Action,
                            Reason = x.Reason
                        })
                        .ToList()
                };
            })
            .ToList();

        return new LegacyMediaCleanupReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            DryRun = options.DryRun,
            Modules = modules,
            Totals = new LegacyMediaCleanupTotals
            {
                Candidates = modules.Sum(x => x.Candidates),
                PlannedDeletes = modules.Sum(x => x.PlannedDeletes),
                PlannedClears = modules.Sum(x => x.PlannedClears),
                AppliedDeletes = modules.Sum(x => x.AppliedDeletes),
                AppliedClears = modules.Sum(x => x.AppliedClears),
                SkippedStoragePresent = modules.Sum(x => x.SkippedStoragePresent),
                SkippedStorageError = modules.Sum(x => x.SkippedStorageError),
                SkippedNoCleanupTarget = modules.Sum(x => x.SkippedNoCleanupTarget)
            }
        };
    }

    private async Task<List<LegacyMediaReference>> LoadLegacyReferencesAsync(CancellationToken cancellationToken)
    {
        var references = new List<LegacyMediaReference>();

        if (await CanQueryTableAsync("property_images", cancellationToken))
        {
            var propertyImagesHaveMediaAssetId = await CanQueryColumnAsync("property_images", "media_asset_id", cancellationToken);
            if (propertyImagesHaveMediaAssetId)
            {
                references.AddRange((await _dbContext.PropertyImages
                        .AsNoTracking()
                        .Where(x => x.ObjectKey != string.Empty)
                        .Select(x => new
                        {
                            x.Id,
                            x.MediaAssetId,
                            x.ObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .Select(x => CreateReference(
                        "PropertyImages",
                        nameof(_dbContext.PropertyImages),
                        x.Id,
                        nameof(x.ObjectKey),
                        x.ObjectKey,
                        x.MediaAssetId)));
            }
            else
            {
                references.AddRange((await _dbContext.PropertyImages
                        .AsNoTracking()
                        .Where(x => x.ObjectKey != string.Empty)
                        .Select(x => new
                        {
                            x.Id,
                            x.ObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .Select(x => CreateReference(
                        "PropertyImages",
                        nameof(_dbContext.PropertyImages),
                        x.Id,
                        nameof(x.ObjectKey),
                        x.ObjectKey,
                        null)));
            }
        }

        if (await CanQueryTableAsync("rooming_house_legal_documents", cancellationToken))
        {
            var legalDocumentsHaveMediaAssetIds =
                await CanQueryColumnAsync("rooming_house_legal_documents", "front_media_asset_id", cancellationToken) &&
                await CanQueryColumnAsync("rooming_house_legal_documents", "back_media_asset_id", cancellationToken) &&
                await CanQueryColumnAsync("rooming_house_legal_documents", "extra_media_asset_id", cancellationToken);
            if (legalDocumentsHaveMediaAssetIds)
            {
                references.AddRange((await _dbContext.RoomingHouseLegalDocuments
                        .AsNoTracking()
                        .Select(x => new
                        {
                            x.RoomingHouseId,
                            x.FrontMediaAssetId,
                            x.BackMediaAssetId,
                            x.ExtraMediaAssetId,
                            x.FrontImageObjectKey,
                            x.BackImageObjectKey,
                            x.ExtraImageObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .SelectMany(x => new[]
                    {
                        CreateReference("LegalDocuments", nameof(_dbContext.RoomingHouseLegalDocuments), x.RoomingHouseId, nameof(x.FrontImageObjectKey), x.FrontImageObjectKey, x.FrontMediaAssetId),
                        CreateReference("LegalDocuments", nameof(_dbContext.RoomingHouseLegalDocuments), x.RoomingHouseId, nameof(x.BackImageObjectKey), x.BackImageObjectKey, x.BackMediaAssetId),
                        CreateReference("LegalDocuments", nameof(_dbContext.RoomingHouseLegalDocuments), x.RoomingHouseId, nameof(x.ExtraImageObjectKey), x.ExtraImageObjectKey, x.ExtraMediaAssetId)
                    }));
            }
            else
            {
                references.AddRange((await _dbContext.RoomingHouseLegalDocuments
                        .AsNoTracking()
                        .Select(x => new
                        {
                            x.RoomingHouseId,
                            x.FrontImageObjectKey,
                            x.BackImageObjectKey,
                            x.ExtraImageObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .SelectMany(x => new[]
                    {
                        CreateReference("LegalDocuments", nameof(_dbContext.RoomingHouseLegalDocuments), x.RoomingHouseId, nameof(x.FrontImageObjectKey), x.FrontImageObjectKey, null),
                        CreateReference("LegalDocuments", nameof(_dbContext.RoomingHouseLegalDocuments), x.RoomingHouseId, nameof(x.BackImageObjectKey), x.BackImageObjectKey, null),
                        CreateReference("LegalDocuments", nameof(_dbContext.RoomingHouseLegalDocuments), x.RoomingHouseId, nameof(x.ExtraImageObjectKey), x.ExtraImageObjectKey, null)
                    }));
            }
        }

        if (await CanQueryTableAsync("kyc_verifications", cancellationToken))
        {
            var kycHasMediaAssetIds =
                await CanQueryColumnAsync("kyc_verifications", "front_media_asset_id", cancellationToken) &&
                await CanQueryColumnAsync("kyc_verifications", "back_media_asset_id", cancellationToken) &&
                await CanQueryColumnAsync("kyc_verifications", "selfie_media_asset_id", cancellationToken);
            if (kycHasMediaAssetIds)
            {
                references.AddRange((await _dbContext.KycVerifications
                        .AsNoTracking()
                        .Select(x => new
                        {
                            x.Id,
                            x.FrontMediaAssetId,
                            x.BackMediaAssetId,
                            x.SelfieMediaAssetId,
                            x.FrontImageObjectKey,
                            x.BackImageObjectKey,
                            x.SelfieImageObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .SelectMany(x => new[]
                    {
                        CreateReference("Kyc", nameof(_dbContext.KycVerifications), x.Id, nameof(x.FrontImageObjectKey), x.FrontImageObjectKey, x.FrontMediaAssetId),
                        CreateReference("Kyc", nameof(_dbContext.KycVerifications), x.Id, nameof(x.BackImageObjectKey), x.BackImageObjectKey, x.BackMediaAssetId),
                        CreateReference("Kyc", nameof(_dbContext.KycVerifications), x.Id, nameof(x.SelfieImageObjectKey), x.SelfieImageObjectKey, x.SelfieMediaAssetId)
                    }));
            }
            else
            {
                references.AddRange((await _dbContext.KycVerifications
                        .AsNoTracking()
                        .Select(x => new
                        {
                            x.Id,
                            x.FrontImageObjectKey,
                            x.BackImageObjectKey,
                            x.SelfieImageObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .SelectMany(x => new[]
                    {
                        CreateReference("Kyc", nameof(_dbContext.KycVerifications), x.Id, nameof(x.FrontImageObjectKey), x.FrontImageObjectKey, null),
                        CreateReference("Kyc", nameof(_dbContext.KycVerifications), x.Id, nameof(x.BackImageObjectKey), x.BackImageObjectKey, null),
                        CreateReference("Kyc", nameof(_dbContext.KycVerifications), x.Id, nameof(x.SelfieImageObjectKey), x.SelfieImageObjectKey, null)
                    }));
            }
        }

        if (await CanQueryTableAsync("contract_files", cancellationToken))
        {
            var contractFilesHaveMediaAssetId = await CanQueryColumnAsync("contract_files", "media_asset_id", cancellationToken);
            if (contractFilesHaveMediaAssetId)
            {
                references.AddRange((await _dbContext.ContractFiles
                        .AsNoTracking()
                        .Where(x => x.StorageObjectKey != string.Empty)
                        .Select(x => new
                        {
                            x.Id,
                            x.MediaAssetId,
                            x.StorageObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .Select(x => CreateReference(
                        "ContractFiles",
                        nameof(_dbContext.ContractFiles),
                        x.Id,
                        nameof(x.StorageObjectKey),
                        x.StorageObjectKey,
                        x.MediaAssetId)));
            }
            else
            {
                references.AddRange((await _dbContext.ContractFiles
                        .AsNoTracking()
                        .Where(x => x.StorageObjectKey != string.Empty)
                        .Select(x => new
                        {
                            x.Id,
                            x.StorageObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .Select(x => CreateReference(
                        "ContractFiles",
                        nameof(_dbContext.ContractFiles),
                        x.Id,
                        nameof(x.StorageObjectKey),
                        x.StorageObjectKey,
                        null)));
            }
        }

        if (await CanQueryTableAsync("contract_occupant_documents", cancellationToken))
        {
            var contractOccupantDocumentsHaveMediaAssetIds =
                await CanQueryColumnAsync("contract_occupant_documents", "front_media_asset_id", cancellationToken) &&
                await CanQueryColumnAsync("contract_occupant_documents", "back_media_asset_id", cancellationToken) &&
                await CanQueryColumnAsync("contract_occupant_documents", "extra_media_asset_id", cancellationToken);
            if (contractOccupantDocumentsHaveMediaAssetIds)
            {
                references.AddRange((await _dbContext.ContractOccupantDocuments
                        .AsNoTracking()
                        .Select(x => new
                        {
                            x.Id,
                            x.FrontMediaAssetId,
                            x.BackMediaAssetId,
                            x.ExtraMediaAssetId,
                            x.FrontImageObjectKey,
                            x.BackImageObjectKey,
                            x.ExtraImageObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .SelectMany(x => new[]
                    {
                        CreateReference("ContractOccupantDocuments", nameof(_dbContext.ContractOccupantDocuments), x.Id, nameof(x.FrontImageObjectKey), x.FrontImageObjectKey, x.FrontMediaAssetId),
                        CreateReference("ContractOccupantDocuments", nameof(_dbContext.ContractOccupantDocuments), x.Id, nameof(x.BackImageObjectKey), x.BackImageObjectKey, x.BackMediaAssetId),
                        CreateReference("ContractOccupantDocuments", nameof(_dbContext.ContractOccupantDocuments), x.Id, nameof(x.ExtraImageObjectKey), x.ExtraImageObjectKey, x.ExtraMediaAssetId)
                    }));
            }
            else
            {
                references.AddRange((await _dbContext.ContractOccupantDocuments
                        .AsNoTracking()
                        .Select(x => new
                        {
                            x.Id,
                            x.FrontImageObjectKey,
                            x.BackImageObjectKey,
                            x.ExtraImageObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .SelectMany(x => new[]
                    {
                        CreateReference("ContractOccupantDocuments", nameof(_dbContext.ContractOccupantDocuments), x.Id, nameof(x.FrontImageObjectKey), x.FrontImageObjectKey, null),
                        CreateReference("ContractOccupantDocuments", nameof(_dbContext.ContractOccupantDocuments), x.Id, nameof(x.BackImageObjectKey), x.BackImageObjectKey, null),
                        CreateReference("ContractOccupantDocuments", nameof(_dbContext.ContractOccupantDocuments), x.Id, nameof(x.ExtraImageObjectKey), x.ExtraImageObjectKey, null)
                    }));
            }
        }

        if (await CanQueryTableAsync("meter_readings", cancellationToken))
        {
            var meterReadingsHaveMediaAssetId = await CanQueryColumnAsync("meter_readings", "proof_media_asset_id", cancellationToken);
            if (meterReadingsHaveMediaAssetId)
            {
                references.AddRange((await _dbContext.MeterReadings
                        .AsNoTracking()
                        .Where(x => x.ProofImageObjectKey != null)
                        .Select(x => new
                        {
                            x.Id,
                            x.ProofMediaAssetId,
                            x.ProofImageObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .Select(x => CreateReference(
                        "MeterReadingProofs",
                        nameof(_dbContext.MeterReadings),
                        x.Id,
                        nameof(x.ProofImageObjectKey),
                        x.ProofImageObjectKey,
                        x.ProofMediaAssetId)));
            }
            else
            {
                references.AddRange((await _dbContext.MeterReadings
                        .AsNoTracking()
                        .Where(x => x.ProofImageObjectKey != null)
                        .Select(x => new
                        {
                            x.Id,
                            x.ProofImageObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .Select(x => CreateReference(
                        "MeterReadingProofs",
                        nameof(_dbContext.MeterReadings),
                        x.Id,
                        nameof(x.ProofImageObjectKey),
                        x.ProofImageObjectKey,
                        null)));
            }
        }

        if (await CanQueryTableAsync("rooming_house_rules", cancellationToken))
        {
            var roomingHouseRulesHaveMediaAssetId = await CanQueryColumnAsync("rooming_house_rules", "media_asset_id", cancellationToken);
            if (roomingHouseRulesHaveMediaAssetId)
            {
                references.AddRange((await _dbContext.RoomingHouseRules
                        .AsNoTracking()
                        .Where(x => x.PdfObjectKey != string.Empty)
                        .Select(x => new
                        {
                            x.Id,
                            x.MediaAssetId,
                            x.PdfObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .Select(x => CreateReference(
                        "RoomingHouseRules",
                        nameof(_dbContext.RoomingHouseRules),
                        x.Id,
                        nameof(x.PdfObjectKey),
                        x.PdfObjectKey,
                        x.MediaAssetId)));
            }
            else
            {
                references.AddRange((await _dbContext.RoomingHouseRules
                        .AsNoTracking()
                        .Where(x => x.PdfObjectKey != string.Empty)
                        .Select(x => new
                        {
                            x.Id,
                            x.PdfObjectKey
                        })
                        .ToListAsync(cancellationToken))
                    .Select(x => CreateReference(
                        "RoomingHouseRules",
                        nameof(_dbContext.RoomingHouseRules),
                        x.Id,
                        nameof(x.PdfObjectKey),
                        x.PdfObjectKey,
                        null)));
            }
        }

        if (await CanQueryTableAsync("users", cancellationToken))
        {
            var usersHaveAvatarMediaAssetId = await CanQueryColumnAsync("users", "avatar_media_asset_id", cancellationToken);
            if (usersHaveAvatarMediaAssetId)
            {
                references.AddRange((await _dbContext.Users
                        .AsNoTracking()
                        .Where(x => x.AvatarUrl != null || x.AvatarMediaAssetId != null)
                        .Select(x => new
                        {
                            x.Id,
                            x.AvatarMediaAssetId,
                            x.AvatarUrl
                        })
                        .ToListAsync(cancellationToken))
                    .Where(x => LooksLikeLocalMediaReference(x.AvatarUrl))
                    .Select(x => CreateReference(
                        "Avatars",
                        nameof(_dbContext.Users),
                        x.Id,
                        nameof(x.AvatarUrl),
                        x.AvatarUrl,
                        x.AvatarMediaAssetId)));
            }
            else
            {
                references.AddRange((await _dbContext.Users
                        .AsNoTracking()
                        .Where(x => x.AvatarUrl != null)
                        .Select(x => new
                        {
                            x.Id,
                            x.AvatarUrl
                        })
                        .ToListAsync(cancellationToken))
                    .Where(x => LooksLikeLocalMediaReference(x.AvatarUrl))
                    .Select(x => CreateReference(
                        "Avatars",
                        nameof(_dbContext.Users),
                        x.Id,
                        nameof(x.AvatarUrl),
                        x.AvatarUrl,
                        null)));
            }
        }

        return references
            .Where(x => !string.IsNullOrWhiteSpace(x.ObjectKey))
            .ToList();
    }

    private static LegacyMediaModuleReport BuildModuleReport(
        IGrouping<string, EnrichedLegacyMediaReference> group,
        LegacyMediaMigrationReadinessOptions options)
    {
        var references = group.ToList();
        var storageChecked = options.CheckStorage;

        return new LegacyMediaModuleReport
        {
            Module = group.Key,
            LegacyReferences = references.Count,
            MissingMediaAssetLinks = references.Count(x => !x.Reference.ExistingMediaAssetId.HasValue),
            ExistingMediaAssetLinks = references.Count(x => x.Reference.ExistingMediaAssetId.HasValue),
            MatchingMediaAssetsByObjectKey = references.Count(x => x.MatchingMediaAssetId.HasValue),
            MissingMediaAssetsByObjectKey = references.Count(x => !x.MatchingMediaAssetId.HasValue),
            StorageChecked = storageChecked,
            StoragePresent = references.Count(x => x.StorageStatus == "Present"),
            StorageMissing = references.Count(x => x.StorageStatus == "Missing"),
            StorageErrors = references.Count(x => x.StorageStatus == "Error"),
            Samples = references
                .Where(x => !x.Reference.ExistingMediaAssetId.HasValue || !x.MatchingMediaAssetId.HasValue || x.StorageStatus is "Missing" or "Error")
                .Take(Math.Max(0, options.SampleLimitPerModule))
                .Select(x => new LegacyMediaReferenceSample
                {
                    Module = x.Reference.Module,
                    EntityName = x.Reference.EntityName,
                    EntityId = x.Reference.EntityId,
                    FieldName = x.Reference.FieldName,
                    ObjectKey = x.Reference.ObjectKey,
                    ExistingMediaAssetId = x.Reference.ExistingMediaAssetId,
                    MatchingMediaAssetId = x.MatchingMediaAssetId,
                    StorageStatus = x.StorageStatus,
                    Note = x.Note
                })
                .ToList()
        };
    }

    private static LegacyMediaReference CreateReference(
        string module,
        string entityName,
        Guid entityId,
        string fieldName,
        string? objectKey,
        Guid? existingMediaAssetId)
    {
        return new LegacyMediaReference(
            module,
            entityName,
            entityId.ToString("D"),
            fieldName,
            NormalizeLegacyObjectKey(objectKey),
            existingMediaAssetId);
    }

    private static bool LooksLikeLocalMediaReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Replace('\\', '/').Trim();
        return normalized.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("/api/media/public/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("api/media/public/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("public/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("private/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLegacyObjectKey(string? objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return string.Empty;
        }

        var normalized = objectKey.Replace('\\', '/').Trim().TrimStart('/');

        if (normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["uploads/".Length..];
        }

        if (normalized.StartsWith("api/media/public/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["api/media/public/".Length..];
        }

        return normalized.TrimStart('/');
    }

    private static LegacyMediaBackfillTarget? ResolveBackfillTarget(LegacyMediaReference reference)
    {
        return reference.Module switch
        {
            "PropertyImages" => new LegacyMediaBackfillTarget(
                "property_images",
                "id",
                "media_asset_id",
                InferPropertyImageScope(reference.ObjectKey),
                MediaVisibility.Public,
                "PropertyImage"),
            "LegalDocuments" => new LegacyMediaBackfillTarget(
                "rooming_house_legal_documents",
                "rooming_house_id",
                reference.FieldName switch
                {
                    "FrontImageObjectKey" => "front_media_asset_id",
                    "BackImageObjectKey" => "back_media_asset_id",
                    "ExtraImageObjectKey" => "extra_media_asset_id",
                    _ => string.Empty
                },
                MediaScope.RoomingHouseLegalDocument,
                MediaVisibility.Private,
                "RoomingHouseLegalDocument"),
            "Kyc" => new LegacyMediaBackfillTarget(
                "kyc_verifications",
                "id",
                reference.FieldName switch
                {
                    "FrontImageObjectKey" => "front_media_asset_id",
                    "BackImageObjectKey" => "back_media_asset_id",
                    "SelfieImageObjectKey" => "selfie_media_asset_id",
                    _ => string.Empty
                },
                MediaScope.KycDocument,
                MediaVisibility.Private,
                "KycVerification"),
            "ContractFiles" => new LegacyMediaBackfillTarget(
                "contract_files",
                "id",
                "media_asset_id",
                MediaScope.ContractPdf,
                MediaVisibility.Private,
                "ContractFile"),
            "ContractOccupantDocuments" => new LegacyMediaBackfillTarget(
                "contract_occupant_documents",
                "id",
                reference.FieldName switch
                {
                    "FrontImageObjectKey" => "front_media_asset_id",
                    "BackImageObjectKey" => "back_media_asset_id",
                    "ExtraImageObjectKey" => "extra_media_asset_id",
                    _ => string.Empty
                },
                MediaScope.KycDocument,
                MediaVisibility.Private,
                "ContractOccupantDocument"),
            "MeterReadingProofs" => new LegacyMediaBackfillTarget(
                "meter_readings",
                "id",
                "proof_media_asset_id",
                MediaScope.MeterReadingImage,
                MediaVisibility.Private,
                "MeterReading"),
            "RoomingHouseRules" => new LegacyMediaBackfillTarget(
                "rooming_house_rules",
                "id",
                "media_asset_id",
                MediaScope.RoomingHouseRulePdf,
                MediaVisibility.Private,
                "RoomingHouseRule"),
            "Avatars" => new LegacyMediaBackfillTarget(
                "users",
                "id",
                "avatar_media_asset_id",
                MediaScope.Avatar,
                MediaVisibility.Public,
                "User"),
            _ => null
        };
    }

    private static LegacyMediaCleanupTarget? ResolveCleanupTarget(LegacyMediaReference reference)
    {
        return reference.Module switch
        {
            "PropertyImages" => LegacyMediaCleanupTarget.Delete(
                reference,
                "property_images",
                "id"),
            "LegalDocuments" => LegacyMediaCleanupTarget.Delete(
                reference,
                "rooming_house_legal_documents",
                "rooming_house_id"),
            "Kyc" => LegacyMediaCleanupTarget.Delete(
                reference,
                "kyc_verifications",
                "id"),
            "ContractFiles" => LegacyMediaCleanupTarget.Delete(
                reference,
                "contract_files",
                "id"),
            "ContractOccupantDocuments" => LegacyMediaCleanupTarget.Delete(
                reference,
                "contract_occupant_documents",
                "id"),
            "MeterReadingProofs" => LegacyMediaCleanupTarget.Clear(
                reference,
                "meter_readings",
                "id",
                "proof_image_object_key",
                "proof_media_asset_id",
                clearObjectKeyToNull: true),
            "RoomingHouseRules" => LegacyMediaCleanupTarget.Clear(
                reference,
                "rooming_house_rules",
                "id",
                "pdf_object_key",
                "media_asset_id",
                clearObjectKeyToNull: false),
            "Avatars" => LegacyMediaCleanupTarget.Clear(
                reference,
                "users",
                "id",
                "avatar_url",
                "avatar_media_asset_id",
                clearObjectKeyToNull: true),
            _ => null
        };
    }

    private static MediaAsset CreateLegacyMediaAsset(
        LegacyMediaReference reference,
        LegacyMediaBackfillTarget target)
    {
        var now = DateTimeOffset.UtcNow;
        var fileName = Path.GetFileName(reference.ObjectKey);

        return new MediaAsset
        {
            Id = Guid.NewGuid(),
            BucketName = "legacy-media",
            ObjectKey = reference.ObjectKey,
            OriginalFileName = string.IsNullOrWhiteSpace(fileName) ? reference.ObjectKey : fileName,
            StoredFileName = string.IsNullOrWhiteSpace(fileName) ? reference.ObjectKey : fileName,
            ContentType = GuessContentType(reference.ObjectKey),
            FileSize = 0,
            Scope = target.Scope,
            Visibility = target.Visibility,
            Status = MediaStatus.Uploaded,
            LinkedEntityType = target.LinkedEntityType,
            LinkedEntityId = Guid.Parse(reference.EntityId),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static MediaScope InferPropertyImageScope(string objectKey)
    {
        return objectKey.Contains("/rooms/", StringComparison.OrdinalIgnoreCase) ||
               objectKey.Contains("room-", StringComparison.OrdinalIgnoreCase)
            ? MediaScope.RoomImage
            : MediaScope.RoomingHouseImage;
    }

    private static string GuessContentType(string objectKey)
    {
        return Path.GetExtension(objectKey).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private async Task<bool> CanQueryColumnAsync(
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        if (_efDbContext is null || !_efDbContext.Database.IsRelational())
        {
            return true;
        }

        var connection = _efDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select exists (
                    select 1
                    from information_schema.columns
                    where table_schema = current_schema()
                      and table_name = @table_name
                      and column_name = @column_name
                )
                """;

            var tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "table_name";
            tableParameter.Value = tableName;
            command.Parameters.Add(tableParameter);

            var columnParameter = command.CreateParameter();
            columnParameter.ParameterName = "column_name";
            columnParameter.Value = columnName;
            command.Parameters.Add(columnParameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is true;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<bool> CanQueryTableAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        if (_efDbContext is null || !_efDbContext.Database.IsRelational())
        {
            return true;
        }

        var connection = _efDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select exists (
                    select 1
                    from information_schema.tables
                    where table_schema = current_schema()
                      and table_name = @table_name
                )
                """;

            var tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "table_name";
            tableParameter.Value = tableName;
            command.Parameters.Add(tableParameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is true;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private sealed record LegacyMediaReference(
        string Module,
        string EntityName,
        string EntityId,
        string FieldName,
        string ObjectKey,
        Guid? ExistingMediaAssetId);

    private sealed record EnrichedLegacyMediaReference(
        LegacyMediaReference Reference,
        Guid? MatchingMediaAssetId,
        string StorageStatus,
        string? Note);

    private sealed record LegacyMediaBackfillTarget(
        string TableName,
        string PrimaryKeyColumnName,
        string LinkColumnName,
        MediaScope Scope,
        MediaVisibility Visibility,
        string LinkedEntityType);

    private sealed record LegacyMediaCleanupTarget(
        string TableName,
        string PrimaryKeyColumnName,
        string EntityId,
        string ObjectKeyColumnName,
        string? LinkColumnName,
        bool DeleteRow,
        bool ClearObjectKeyToNull)
    {
        public string OperationKey =>
            $"{TableName}:{PrimaryKeyColumnName}:{EntityId}:{(DeleteRow ? "delete" : ObjectKeyColumnName)}";

        public static LegacyMediaCleanupTarget Delete(
            LegacyMediaReference reference,
            string tableName,
            string primaryKeyColumnName)
        {
            return new LegacyMediaCleanupTarget(
                tableName,
                primaryKeyColumnName,
                reference.EntityId,
                reference.FieldName,
                null,
                DeleteRow: true,
                ClearObjectKeyToNull: true);
        }

        public static LegacyMediaCleanupTarget Clear(
            LegacyMediaReference reference,
            string tableName,
            string primaryKeyColumnName,
            string objectKeyColumnName,
            string? linkColumnName,
            bool clearObjectKeyToNull)
        {
            return new LegacyMediaCleanupTarget(
                tableName,
                primaryKeyColumnName,
                reference.EntityId,
                objectKeyColumnName,
                linkColumnName,
                DeleteRow: false,
                ClearObjectKeyToNull: clearObjectKeyToNull);
        }
    }

    private sealed record LegacyMediaBackfillAction(
        LegacyMediaReference Reference,
        LegacyMediaBackfillTarget? Target,
        string Action,
        string? Reason,
        Guid? TargetMediaAssetId,
        bool CreateMediaAsset,
        MediaAsset? MediaAsset)
    {
        public static LegacyMediaBackfillAction Skip(
            LegacyMediaReference reference,
            string action,
            string reason)
        {
            return new LegacyMediaBackfillAction(reference, null, action, reason, null, false, null);
        }

        public static LegacyMediaBackfillAction Link(
            LegacyMediaReference reference,
            LegacyMediaBackfillTarget target,
            Guid targetMediaAssetId,
            bool createMediaAsset,
            MediaAsset? mediaAsset = null)
        {
            return new LegacyMediaBackfillAction(
                reference,
                target,
                "Link",
                createMediaAsset ? "Create MediaAsset and link legacy row." : "Link legacy row to existing MediaAsset.",
                targetMediaAssetId,
                createMediaAsset,
                mediaAsset);
        }
    }

    private sealed record LegacyMediaCleanupAction(
        LegacyMediaReference Reference,
        LegacyMediaCleanupTarget? Target,
        string Action,
        string? Reason)
    {
        public static LegacyMediaCleanupAction Skip(
            LegacyMediaReference reference,
            string action,
            string reason)
        {
            return new LegacyMediaCleanupAction(reference, null, action, reason);
        }

        public static LegacyMediaCleanupAction Cleanup(
            LegacyMediaReference reference,
            LegacyMediaCleanupTarget target)
        {
            return new LegacyMediaCleanupAction(
                reference,
                target,
                target.DeleteRow ? "DeleteRow" : "ClearField",
                target.DeleteRow
                    ? "Delete legacy media row because its storage object is missing."
                    : "Clear legacy media fields because their storage object is missing.");
        }
    }
}
