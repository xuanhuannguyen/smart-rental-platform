using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.Billing;

public sealed class BillingContractContextResolver
{
    private readonly IAppDbContext context;

    public BillingContractContextResolver(IAppDbContext context)
    {
        this.context = context;
    }

    internal async Task<ResolvedInvoiceTenant> ResolveEffectiveInvoiceTenantAsync(
        Guid contractId,
        Guid currentContractTenantUserId,
        DateOnly effectiveOn,
        CancellationToken cancellationToken)
    {
        var tenantChanges = await context.ContractAppendixChanges
            .AsNoTracking()
            .Where(x => x.RentalContractAppendix.RentalContractId == contractId &&
                        x.RentalContractAppendix.AppliedAt != null &&
                        x.TargetType == ContractAppendixTargetType.Contract &&
                        x.ChangeType == ContractAppendixChangeType.Update &&
                        x.FieldName != null)
            .Select(x => new
            {
                x.RentalContractAppendix.EffectiveDate,
                x.SortOrder,
                x.OldValue,
                x.NewValue,
                x.FieldName
            })
            .ToListAsync(cancellationToken);

        var mainTenantChanges = tenantChanges
            .Where(x => NormalizeAppendixFieldName(x.FieldName) == "maintenantuserid")
            .OrderBy(x => x.EffectiveDate)
            .ThenBy(x => x.SortOrder)
            .ToList();

        var effectiveTenantUserId = currentContractTenantUserId;
        var latestAppliedChange = mainTenantChanges
            .Where(x => x.EffectiveDate <= effectiveOn)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.SortOrder)
            .FirstOrDefault();

        if (latestAppliedChange is not null &&
            TryExtractGuid(latestAppliedChange.NewValue, out var appliedTenantUserId))
        {
            effectiveTenantUserId = appliedTenantUserId;
        }
        else if (mainTenantChanges.Count > 0 &&
                 effectiveOn < mainTenantChanges[0].EffectiveDate &&
                 TryExtractGuid(mainTenantChanges[0].OldValue, out var oldTenantUserId))
        {
            effectiveTenantUserId = oldTenantUserId;
        }

        var tenant = await context.Users
            .AsNoTracking()
            .Where(x => x.Id == effectiveTenantUserId && x.DeletedAt == null)
            .Select(x => new ResolvedInvoiceTenant(x.Id, x.DisplayName, x.Email))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                ErrorCodes.NotFound,
                "Không tìm thấy người thuê chính hiện tại của hợp đồng.");

        return tenant;
    }

    internal async Task<ResolvedInvoiceContractContext> ResolveInvoiceContractContextAsync(
        Guid contractId,
        Guid currentContractTenantUserId,
        decimal currentMonthlyRent,
        ResolvedBillingPeriod billingPeriod,
        DateOnly tenantEffectiveOn,
        CancellationToken cancellationToken)
    {
        var monthlyRent = await ResolveEffectiveMonthlyRentAsync(
            contractId,
            currentMonthlyRent,
            billingPeriod.Start,
            cancellationToken);

        var effectiveTenant = await ResolveEffectiveInvoiceTenantAsync(
            contractId,
            currentContractTenantUserId,
            tenantEffectiveOn,
            cancellationToken);

        var occupantCount = await GetActiveOccupantCountAsync(
            contractId,
            billingPeriod,
            cancellationToken);

        return new ResolvedInvoiceContractContext(
            effectiveTenant,
            monthlyRent,
            occupantCount);
    }

    internal async Task<ResolvedContractTerms> ResolveEffectiveContractTermsAsync(
        Guid contractId,
        DateOnly currentContractStartDate,
        DateOnly currentContractEndDate,
        DateOnly effectiveOn,
        CancellationToken cancellationToken)
    {
        var termChanges = await context.ContractAppendixChanges
            .AsNoTracking()
            .Where(x => x.RentalContractAppendix.RentalContractId == contractId &&
                        x.RentalContractAppendix.AppliedAt != null &&
                        x.TargetType == ContractAppendixTargetType.Contract &&
                        x.ChangeType == ContractAppendixChangeType.Update &&
                        x.FieldName != null)
            .Select(x => new
            {
                x.RentalContractAppendix.EffectiveDate,
                x.SortOrder,
                x.OldValue,
                x.NewValue,
                x.FieldName
            })
            .ToListAsync(cancellationToken);

        var startDate = ResolveEffectiveAppendixDate(
            termChanges
                .Where(x => NormalizeAppendixFieldName(x.FieldName) == "startdate")
                .Select(x => new AppendixDateChange(x.EffectiveDate, x.SortOrder, x.OldValue, x.NewValue)),
            currentContractStartDate,
            effectiveOn);
        var endDate = ResolveEffectiveAppendixDate(
            termChanges
                .Where(x => NormalizeAppendixFieldName(x.FieldName) == "enddate")
                .Select(x => new AppendixDateChange(x.EffectiveDate, x.SortOrder, x.OldValue, x.NewValue)),
            currentContractEndDate,
            effectiveOn);

        return new ResolvedContractTerms(startDate, endDate);
    }

    internal async Task<decimal> ResolveEffectiveMonthlyRentAsync(
        Guid contractId,
        decimal currentContractMonthlyRent,
        DateOnly effectiveOn,
        CancellationToken cancellationToken)
    {
        var rentChanges = await context.ContractAppendixChanges
            .AsNoTracking()
            .Where(x => x.RentalContractAppendix.RentalContractId == contractId &&
                        x.RentalContractAppendix.AppliedAt != null &&
                        x.TargetType == ContractAppendixTargetType.Contract &&
                        x.ChangeType == ContractAppendixChangeType.Update &&
                        x.FieldName != null &&
                        x.FieldName.ToLower() == "monthlyrent")
            .Select(x => new
            {
                x.RentalContractAppendix.EffectiveDate,
                x.OldValue,
                x.NewValue
            })
            .OrderBy(x => x.EffectiveDate)
            .ToListAsync(cancellationToken);

        if (rentChanges.Count == 0)
        {
            return currentContractMonthlyRent;
        }

        var latestAppliedChange = rentChanges
            .Where(x => x.EffectiveDate <= effectiveOn)
            .OrderByDescending(x => x.EffectiveDate)
            .FirstOrDefault();
        if (latestAppliedChange is not null &&
            TryParseAppendixDecimal(latestAppliedChange.NewValue, out var appliedRent))
        {
            return appliedRent;
        }

        var firstChange = rentChanges[0];
        if (effectiveOn < firstChange.EffectiveDate &&
            TryParseAppendixDecimal(firstChange.OldValue, out var oldRent))
        {
            return oldRent;
        }

        return currentContractMonthlyRent;
    }

    private async Task<int> GetActiveOccupantCountAsync(
        Guid contractId,
        ResolvedBillingPeriod period,
        CancellationToken cancellationToken)
    {
        var count = await context.ContractOccupants.CountAsync(
            x => x.RentalContractId == contractId &&
                 (x.Status == ContractOccupantStatus.Active ||
                  x.Status == ContractOccupantStatus.PendingMoveIn ||
                  x.Status == ContractOccupantStatus.MoveOut) &&
                 x.MoveInDate <= period.End &&
                 (x.MoveOutDate == null || x.MoveOutDate >= period.Start),
            cancellationToken);

        return Math.Max(count, 1);
    }

    private static DateOnly ResolveEffectiveAppendixDate(
        IEnumerable<AppendixDateChange> changes,
        DateOnly currentValue,
        DateOnly effectiveOn)
    {
        var orderedChanges = changes
            .OrderBy(x => x.EffectiveDate)
            .ThenBy(x => x.SortOrder)
            .ToList();

        if (orderedChanges.Count == 0)
        {
            return currentValue;
        }

        var latestAppliedChange = orderedChanges
            .Where(x => x.EffectiveDate <= effectiveOn)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.SortOrder)
            .FirstOrDefault();
        if (latestAppliedChange is not null &&
            TryParseAppendixDate(latestAppliedChange.NewValue, out var appliedValue))
        {
            return appliedValue;
        }

        var firstChange = orderedChanges[0];
        if (effectiveOn < firstChange.EffectiveDate &&
            TryParseAppendixDate(firstChange.OldValue, out var oldValue))
        {
            return oldValue;
        }

        return currentValue;
    }

    private static bool TryParseAppendixDecimal(string? value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Trim('"');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseAppendixDate(string? value, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Trim('"');
        return DateOnly.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out result) ||
               DateOnly.TryParse(normalized, out result);
    }

    private static string NormalizeAppendixFieldName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static bool TryExtractGuid(string? value, out Guid result)
    {
        result = Guid.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim().Trim('"');
        if (Guid.TryParse(trimmed, out result))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return Guid.TryParse(root.GetString(), out result);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var propertyName in new[] { "id", "userId", "tenantUserId", "mainTenantUserId", "value" })
            {
                if (root.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(property.GetString(), out result))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private sealed record AppendixDateChange(
        DateOnly EffectiveDate,
        int SortOrder,
        string? OldValue,
        string? NewValue);
}

internal sealed record ResolvedInvoiceTenant(
    Guid UserId,
    string DisplayName,
    string Email);

internal sealed record ResolvedInvoiceContractContext(
    ResolvedInvoiceTenant EffectiveTenant,
    decimal MonthlyRent,
    int OccupantCount);

internal sealed record ResolvedContractTerms(
    DateOnly StartDate,
    DateOnly EndDate);
