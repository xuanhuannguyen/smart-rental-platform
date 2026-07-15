using System.Text.Json;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Contracts.RentalContracts.Responses;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class RentalContractResponseMapper
{
    public static ContractDetailResponse ToDetailResponse(RentalContract contract)
    {
        ApplyCurrentContractTerms(contract);
        return new ContractDetailResponse
        {
            Id = contract.Id,
            RentalRequestId = contract.RentalRequestId,
            RoomDepositId = contract.RoomDepositId,
            RoomId = contract.RoomId,
            RoomNumber = contract.Room.RoomNumber,
            RoomingHouseId = contract.Room.RoomingHouseId,
            RoomingHouseName = contract.Room.RoomingHouse.Name,
            MainTenantUserId = contract.MainTenantUserId,
            MainTenantName = contract.MainTenantUser.DisplayName,
            ContractNumber = contract.ContractNumber,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            MonthlyRent = contract.MonthlyRent,
            DepositAmount = contract.DepositAmount,
            PaymentDay = contract.PaymentDay,
            Status = contract.Status.ToString(),
            RoomSnapshot = contract.RoomSnapshot,
            SignatureDeadlineAt = contract.SignatureDeadlineAt,
            ActivatedAt = contract.ActivatedAt,
            TerminationDate = contract.TerminationDate,
            TerminationType = contract.TerminationType?.ToString(),
            StatusReason = contract.StatusReason,
            Occupants = contract.Occupants.OrderBy(x => x.CreatedAt).Select(ToOccupantResponse).ToList(),
            Signatures = contract.Signatures.OrderBy(x => x.SignedAt).Select(ToSignatureResponse).ToList(),
            CreatedAt = contract.CreatedAt,
            UpdatedAt = contract.UpdatedAt
        };
    }

    public static ContractHistoryItemResponse ToHistoryItemResponse(RentalContract contract, Guid userId)
    {
        var currentMainTenantUserId = GetCurrentMainTenantUserId(contract);
        var mainTenantUserIds = GetMainTenantUserIds(contract);
        var snapshotBoundaryAppendix = ResolveHistorySnapshotBoundaryAppendix(contract, userId);
        ApplyContractTerms(contract, snapshotBoundaryAppendix);
        var occupants = ResolveOccupantsForHistorySnapshot(contract, snapshotBoundaryAppendix).ToList();
        var isLandlord = contract.Room.RoomingHouse.LandlordUserId == userId;
        var isMainTenant = currentMainTenantUserId == userId;
        var wasMainTenant = mainTenantUserIds.Contains(userId);
        var currentUserOccupant = occupants
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Status == ContractOccupantStatus.Active)
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefault();
        var isFormerMainTenant = wasMainTenant && !isMainTenant;
        var isCoTenant = currentUserOccupant is not null &&
            currentUserOccupant.Status == ContractOccupantStatus.Active &&
            !isMainTenant;
        var isFormerCoTenant = currentUserOccupant is not null &&
            currentUserOccupant.Status != ContractOccupantStatus.Active &&
            !wasMainTenant;
        var canViewRawContract = isLandlord || wasMainTenant;
        var canViewMaskedContract = !canViewRawContract && currentUserOccupant is not null;
        var canMainTenantAct = isMainTenant && contract.Status == RentalContractStatus.Active;

        return new ContractHistoryItemResponse
        {
            Id = contract.Id,
            RentalRequestId = contract.RentalRequestId,
            RoomId = contract.RoomId,
            RoomNumber = contract.Room.RoomNumber,
            RoomingHouseId = contract.Room.RoomingHouseId,
            RoomingHouseName = contract.Room.RoomingHouse.Name,
            MainTenantUserId = contract.MainTenantUserId,
            MainTenantName = contract.MainTenantUser.DisplayName,
            ContractNumber = contract.ContractNumber,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            MonthlyRent = contract.MonthlyRent,
            DepositAmount = contract.DepositAmount,
            PaymentDay = contract.PaymentDay,
            MaxOccupants = GetSnapshotMaxOccupants(contract),
            Status = contract.Status.ToString(),
            StatusReason = contract.StatusReason,
            SignatureDeadlineAt = contract.SignatureDeadlineAt,
            ActivatedAt = contract.ActivatedAt,
            TerminationDate = contract.TerminationDate,
            TerminationType = contract.TerminationType?.ToString(),
            IsMainTenant = isMainTenant,
            WasMainTenant = wasMainTenant,
            IsFormerMainTenant = isFormerMainTenant,
            IsCoTenant = isCoTenant,
            IsFormerCoTenant = isFormerCoTenant,
            CurrentUserRelation = ResolveCurrentUserRelation(
                isMainTenant,
                isFormerMainTenant,
                isCoTenant,
                isFormerCoTenant,
                currentUserOccupant is not null),
            CurrentUserOccupantId = currentUserOccupant?.Id,
            CurrentUserOccupantStatus = currentUserOccupant?.Status.ToString(),
            CurrentUserMoveInDate = currentUserOccupant?.MoveInDate,
            CurrentUserMoveOutDate = currentUserOccupant?.MoveOutDate,
            SnapshotAtAppendixId = snapshotBoundaryAppendix?.Id,
            SnapshotAtDate = snapshotBoundaryAppendix?.EffectiveDate,
            Occupants = occupants.OrderBy(x => x.MoveInDate).ThenBy(x => x.CreatedAt).Select(ToOccupantResponse).ToList(),
            CanViewRawContract = canViewRawContract,
            CanViewMaskedContract = canViewMaskedContract,
            CanCreateAppendix = canMainTenantAct,
            CanTerminateContract = canMainTenantAct,
            CreatedAt = contract.CreatedAt,
            UpdatedAt = contract.UpdatedAt
        };
    }

    public static ContractOccupantResponse ToOccupantResponse(ContractOccupant occupant)
    {
        return new ContractOccupantResponse
        {
            Id = occupant.Id,
            UserId = occupant.UserId,
            Email = occupant.User?.Email,
            GuardianOccupantId = occupant.GuardianOccupantId,
            FullName = occupant.FullName,
            PhoneNumber = occupant.PhoneNumber,
            DateOfBirth = occupant.DateOfBirth,
            RelationshipToMainTenant = occupant.RelationshipToMainTenant,
            MoveInDate = occupant.MoveInDate,
            MoveOutDate = occupant.MoveOutDate,
            Status = occupant.Status.ToString(),
            Document = occupant.Documents.OrderBy(x => x.CreatedAt).Select(ToDocumentResponse).FirstOrDefault()
        };
    }

    public static void ApplyCurrentContractTerms(RentalContract contract)
    {
        ApplyContractTerms(contract, null);
    }

    public static (DateOnly StartDate, DateOnly EndDate) ResolveCurrentContractTermValues(RentalContract contract)
    {
        var startDate = contract.StartDate;
        var endDate = contract.EndDate;
        foreach (var appendix in GetActiveAppendicesInOrder(contract))
        {
            foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (change.TargetType != ContractAppendixTargetType.Contract ||
                    change.ChangeType != ContractAppendixChangeType.Update ||
                    string.IsNullOrWhiteSpace(change.NewValue))
                {
                    continue;
                }

                switch (NormalizeFieldName(change.FieldName))
                {
                    case "startdate":
                        if (DateOnly.TryParse(change.NewValue, out var parsedStartDate))
                        {
                            startDate = parsedStartDate;
                        }
                        break;
                    case "enddate":
                        if (DateOnly.TryParse(change.NewValue, out var parsedEndDate))
                        {
                            endDate = parsedEndDate;
                        }
                        break;
                }
            }
        }

        return (startDate, endDate);
    }

    public static Guid GetCurrentMainTenantUserId(RentalContract contract)
    {
        var result = contract.MainTenantUserId;
        foreach (var appendix in GetActiveAppendicesInOrder(contract))
        {
            foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (!IsMainTenantUserIdChange(change))
                {
                    continue;
                }

                var userId = ExtractUserId(change.NewValue);
                if (userId.HasValue)
                {
                    result = userId.Value;
                }
            }
        }

        return result;
    }

    public static int GetSnapshotMaxOccupants(RentalContract contract)
    {
        if (string.IsNullOrWhiteSpace(contract.RoomSnapshot))
        {
            return contract.Room.MaxOccupants;
        }

        try
        {
            using var jsonDocument = JsonDocument.Parse(contract.RoomSnapshot);
            if (jsonDocument.RootElement.TryGetProperty("MaxOccupants", out var value) &&
                value.TryGetInt32(out var maxOccupants) &&
                maxOccupants > 0)
            {
                return maxOccupants;
            }
        }
        catch (JsonException)
        {
            return contract.Room.MaxOccupants;
        }

        return contract.Room.MaxOccupants;
    }

    private static ContractOccupantDocumentResponse ToDocumentResponse(ContractOccupantDocument document)
    {
        return new ContractOccupantDocumentResponse
        {
            Id = document.Id,
            ContractOccupantId = document.RentalContractOccupantId,
            DocumentType = document.DocumentType,
            DocumentNumberMasked = document.DocumentNumberMasked,
            FrontMediaAssetId = document.FrontMediaAssetId,
            BackMediaAssetId = document.BackMediaAssetId,
            ExtraMediaAssetId = document.ExtraMediaAssetId,
            FrontImageUrl = BuildRequiredPrivateMediaUrl(document.FrontMediaAssetId),
            BackImageUrl = BuildOptionalPrivateMediaUrl(document.BackMediaAssetId),
            ExtraImageUrl = BuildOptionalPrivateMediaUrl(document.ExtraMediaAssetId),
            UploadedAt = document.UploadedAt
        };
    }

    private static ContractSignatureResponse ToSignatureResponse(ContractSignature signature)
    {
        return new ContractSignatureResponse
        {
            Id = signature.Id,
            SignerUserId = signature.SignerUserId,
            SignerRole = signature.SignerRole.ToString(),
            SignatureMethod = signature.SignatureMethod.ToString(),
            SignedAt = signature.SignedAt
        };
    }

    private static void ApplyContractTerms(RentalContract contract, ContractAppendix? boundaryAppendix)
    {
        foreach (var appendix in GetActiveAppendicesInOrder(contract))
        {
            foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (change.TargetType != ContractAppendixTargetType.Contract ||
                    change.ChangeType != ContractAppendixChangeType.Update ||
                    string.IsNullOrWhiteSpace(change.NewValue))
                {
                    continue;
                }

                switch (NormalizeFieldName(change.FieldName))
                {
                    case "monthlyrent":
                        if (decimal.TryParse(change.NewValue, out var monthlyRent))
                        {
                            contract.MonthlyRent = monthlyRent;
                        }
                        break;
                    case "depositamount":
                        if (decimal.TryParse(change.NewValue, out var depositAmount))
                        {
                            contract.DepositAmount = depositAmount;
                        }
                        break;
                    case "paymentday":
                        if (int.TryParse(change.NewValue, out var paymentDay))
                        {
                            contract.PaymentDay = paymentDay;
                        }
                        break;
                    case "startdate":
                        if (DateOnly.TryParse(change.NewValue, out var startDate))
                        {
                            contract.StartDate = startDate;
                        }
                        break;
                    case "enddate":
                        if (DateOnly.TryParse(change.NewValue, out var endDate))
                        {
                            contract.EndDate = endDate;
                        }
                        break;
                    case "maintenantuserid":
                        var userId = ExtractUserId(change.NewValue);
                        if (userId.HasValue)
                        {
                            contract.MainTenantUserId = userId.Value;
                            var user = contract.Occupants.FirstOrDefault(x => x.UserId == userId.Value)?.User;
                            if (user is not null)
                            {
                                contract.MainTenantUser = user;
                            }
                        }
                        break;
                }
            }

            if (boundaryAppendix is not null && appendix.Id == boundaryAppendix.Id)
            {
                break;
            }
        }
    }

    private static ContractAppendix? ResolveHistorySnapshotBoundaryAppendix(RentalContract contract, Guid userId)
    {
        var mainTenantUserId = contract.MainTenantUserId;
        foreach (var appendix in GetActiveAppendicesInOrder(contract))
        {
            var mainTenantUserIdBeforeAppendix = mainTenantUserId;
            var removedCurrentUser = appendix.Changes.Any(change =>
                change.TargetType == ContractAppendixTargetType.ContractOccupant &&
                change.ChangeType == ContractAppendixChangeType.Remove &&
                contract.Occupants.Any(occupant => occupant.Id == change.TargetId && occupant.UserId == userId));

            foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (!IsMainTenantUserIdChange(change))
                {
                    continue;
                }

                var newMainTenantUserId = ExtractUserId(change.NewValue);
                if (!newMainTenantUserId.HasValue)
                {
                    continue;
                }

                if (mainTenantUserIdBeforeAppendix == userId && newMainTenantUserId.Value != userId)
                {
                    return appendix;
                }

                mainTenantUserId = newMainTenantUserId.Value;
            }

            if (removedCurrentUser)
            {
                return appendix;
            }
        }

        return null;
    }

    private static IEnumerable<ContractOccupant> ResolveOccupantsForHistorySnapshot(
        RentalContract contract,
        ContractAppendix? boundaryAppendix)
    {
        if (boundaryAppendix is null)
        {
            return contract.Occupants;
        }

        var excludedOccupantIds = new HashSet<Guid>();
        foreach (var appendix in GetActiveAppendicesAfter(contract, boundaryAppendix))
        {
            foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (change.TargetType != ContractAppendixTargetType.ContractOccupant)
                {
                    continue;
                }

                if (change.ChangeType == ContractAppendixChangeType.Add && change.TargetId.HasValue)
                {
                    excludedOccupantIds.Add(change.TargetId.Value);
                }
                else if (change.ChangeType == ContractAppendixChangeType.Remove && change.TargetId.HasValue)
                {
                    var occupant = contract.Occupants.FirstOrDefault(x => x.Id == change.TargetId.Value);
                    if (occupant is not null)
                    {
                        occupant.Status = ContractOccupantStatus.Active;
                        occupant.MoveOutDate = null;
                    }
                }
            }
        }

        return contract.Occupants.Where(x => !excludedOccupantIds.Contains(x.Id));
    }

    private static string ResolveCurrentUserRelation(
        bool isMainTenant,
        bool isFormerMainTenant,
        bool isCoTenant,
        bool isFormerCoTenant,
        bool hasOccupantRecord)
    {
        if (isMainTenant)
        {
            return "CurrentMainTenant";
        }

        if (isFormerMainTenant)
        {
            return "FormerMainTenant";
        }

        if (isCoTenant)
        {
            return "CoTenant";
        }

        if (isFormerCoTenant)
        {
            return "FormerCoTenant";
        }

        return hasOccupantRecord ? "FormerOccupant" : "HistoricalParticipant";
    }

    private static IReadOnlyCollection<Guid> GetMainTenantUserIds(RentalContract contract)
    {
        var userIds = new HashSet<Guid> { contract.MainTenantUserId };
        foreach (var appendix in GetActiveAppendicesInOrder(contract))
        {
            foreach (var change in appendix.Changes.OrderBy(x => x.SortOrder))
            {
                if (!IsMainTenantUserIdChange(change))
                {
                    continue;
                }

                var userId = ExtractUserId(change.NewValue);
                if (userId.HasValue)
                {
                    userIds.Add(userId.Value);
                }
            }
        }

        return userIds;
    }

    private static bool IsMainTenantUserIdChange(ContractAppendixChange change)
    {
        return change.TargetType == ContractAppendixTargetType.Contract &&
            change.ChangeType == ContractAppendixChangeType.Update &&
            NormalizeFieldName(change.FieldName) == "maintenantuserid";
    }

    private static IEnumerable<ContractAppendix> GetActiveAppendicesInOrder(RentalContract contract)
    {
        return contract.Appendices
            .Where(x => x.AppliedAt.HasValue &&
                (x.Status == ContractAppendixStatus.Active || x.Status == ContractAppendixStatus.Cancelled))
            .OrderBy(x => x.AppliedAt ?? x.ActivatedAt ?? x.UpdatedAt)
            .ThenBy(x => x.CreatedAt);
    }

    private static IEnumerable<ContractAppendix> GetActiveAppendicesAfter(
        RentalContract contract,
        ContractAppendix boundaryAppendix)
    {
        var foundBoundary = false;
        foreach (var appendix in GetActiveAppendicesInOrder(contract))
        {
            if (foundBoundary)
            {
                yield return appendix;
            }
            else
            {
                foundBoundary = appendix.Id == boundaryAppendix.Id;
            }
        }
    }

    private static Guid? ExtractUserId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var input = value.Trim().Trim('"');
        if (Guid.TryParse(input, out var parsedInput))
        {
            return parsedInput;
        }

        try
        {
            using var jsonDocument = JsonDocument.Parse(value);
            var rootElement = jsonDocument.RootElement;
            if (rootElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(rootElement.GetString(), out var parsedString))
            {
                return parsedString;
            }

            if (rootElement.ValueKind == JsonValueKind.Object &&
                rootElement.TryGetProperty("userId", out var userIdProperty) &&
                userIdProperty.ValueKind == JsonValueKind.String &&
                Guid.TryParse(userIdProperty.GetString(), out var parsedUserId))
            {
                return parsedUserId;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string NormalizeFieldName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
    }

    private static string BuildRequiredPrivateMediaUrl(Guid? mediaAssetId)
    {
        if (!mediaAssetId.HasValue)
        {
            throw new InvalidOperationException("Contract occupant document is missing the required front media asset.");
        }

        return PrivateMediaPathBuilder.Build(mediaAssetId.Value);
    }

    private static string? BuildOptionalPrivateMediaUrl(Guid? mediaAssetId)
    {
        return mediaAssetId.HasValue
            ? PrivateMediaPathBuilder.Build(mediaAssetId.Value)
            : null;
    }
}
