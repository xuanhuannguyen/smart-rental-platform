using System.Text.Json;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class ContractAppendixChangeParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ParsedAppendixChange ParseAppendixChange(ContractAppendixChangeRequest request, int sortOrder)
    {
        return new ParsedAppendixChange(
            request,
            ParseEnum<ContractAppendixChangeType>(request.ChangeType, "Loại thay đổi phụ lục không hợp lệ."),
            ParseEnum<ContractAppendixTargetType>(request.TargetType, "Đối tượng thay đổi phụ lục không hợp lệ."),
            sortOrder);
    }

    public static string? ResolveOldValue(
        RentalContract contract,
        ContractAppendixChangeRequest request,
        ContractAppendixChangeType changeType,
        ContractAppendixTargetType targetType)
    {
        if (changeType == ContractAppendixChangeType.Add)
        {
            return null;
        }

        object? value = targetType switch
        {
            ContractAppendixTargetType.Contract => ResolveContractOldValue(contract, request.FieldName),
            ContractAppendixTargetType.ContractOccupant => ResolveOccupantOldValue(contract, request.TargetId, request.FieldName),
            _ => null
        };

        return value is null ? null : JsonSerializer.Serialize(value);
    }

    public static string? NormalizeNewValue(
        ContractAppendixChangeRequest change,
        ContractAppendixChangeType changeType,
        ContractAppendixTargetType targetType)
    {
        if (changeType == ContractAppendixChangeType.Add &&
            targetType == ContractAppendixTargetType.ContractOccupant)
        {
            ContractOccupantRequest occupant = ParseOccupantRequest(change.NewValue);
            return JsonSerializer.Serialize(occupant, JsonOptions);
        }

        return ToJsonString(change.NewValue);
    }

    public static ContractOccupantRequest ParseOccupantRequest(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Thông tin người ở mới trong phụ lục không được để trống.");
        }

        string json = value.Trim();

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.String)
            {
                json = document.RootElement.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // The next deserialize step will return a clearer business error.
        }

        try
        {
            return JsonSerializer.Deserialize<ContractOccupantRequest>(json, JsonOptions)
                ?? throw new BadRequestException(
                    ErrorCodes.RentalContractInvalidOccupant,
                    "Thông tin người ở mới trong phụ lục không hợp lệ.");
        }
        catch (JsonException exception)
        {
            throw new BadRequestException(
                ErrorCodes.RentalContractInvalidOccupant,
                "Thông tin người ở mới trong phụ lục phải là JSON hợp lệ.",
                new { exception.Message });
        }
    }

    public static bool IsMainTenantUserIdChange(ParsedAppendixChange change)
    {
        return change.TargetType == ContractAppendixTargetType.Contract &&
               change.ChangeType == ContractAppendixChangeType.Update &&
               NormalizeFieldName(change.Request.FieldName) == "maintenantuserid";
    }

    public static bool IsRenewalChange(ParsedAppendixChange change)
    {
        return change.TargetType == ContractAppendixTargetType.Contract &&
               change.ChangeType == ContractAppendixChangeType.Update &&
               NormalizeFieldName(change.Request.FieldName) == "enddate";
    }

    public static bool IsMonthlyRentChange(ParsedAppendixChange change)
    {
        return change.TargetType == ContractAppendixTargetType.Contract &&
               change.ChangeType == ContractAppendixChangeType.Update &&
               NormalizeFieldName(change.Request.FieldName) == "monthlyrent";
    }

    public static bool IsPaymentDayChange(ParsedAppendixChange change)
    {
        return change.TargetType == ContractAppendixTargetType.Contract &&
               change.ChangeType == ContractAppendixChangeType.Update &&
               NormalizeFieldName(change.Request.FieldName) == "paymentday";
    }

    public static Guid? ExtractUserId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim().Trim('"');
        if (Guid.TryParse(trimmed, out Guid directGuid))
        {
            return directGuid;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(value);
            JsonElement root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String &&
                Guid.TryParse(root.GetString(), out Guid jsonStringGuid))
            {
                return jsonStringGuid;
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("userId", out JsonElement userIdElement) &&
                userIdElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(userIdElement.GetString(), out Guid objectGuid))
            {
                return objectGuid;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    public static string? ExtractUserEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim().Trim('"');
        if (trimmed.Contains('@', StringComparison.Ordinal))
        {
            return trimmed.ToLowerInvariant();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(value);
            JsonElement root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String)
            {
                string? text = root.GetString()?.Trim();
                return !string.IsNullOrWhiteSpace(text) && text.Contains('@', StringComparison.Ordinal)
                    ? text.ToLowerInvariant()
                    : null;
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("email", out JsonElement emailElement) &&
                emailElement.ValueKind == JsonValueKind.String)
            {
                string? email = emailElement.GetString()?.Trim();
                return string.IsNullOrWhiteSpace(email) ? null : email.ToLowerInvariant();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    public static string? ExtractOccupantEmail(string? value)
    {
        try
        {
            return RentalContractTextHelper.NormalizeOptionalText(ParseOccupantRequest(value).Email)?.ToLowerInvariant();
        }
        catch (BadRequestException)
        {
            return null;
        }
    }

    public static bool TryParseDateOnly(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateOnly.TryParse(value.Trim().Trim('"'), out date);
    }

    public static string? ExtractJsonString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.String
                ? document.RootElement.GetString()
                : value.Trim();
        }
        catch (JsonException)
        {
            return value.Trim().Trim('"');
        }
    }

    public static int CountStartedMonths(DateOnly from, DateOnly to)
    {
        int months = ((to.Year - from.Year) * 12) + to.Month - from.Month;
        if (to.Day > from.Day)
        {
            months++;
        }

        return Math.Max(months, 1);
    }

    public static string NormalizeFieldName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToLowerInvariant();
    }

    public static TEnum ParseEnum<TEnum>(string value, string message)
        where TEnum : struct
    {
        if (Enum.TryParse(value, ignoreCase: true, out TEnum result))
        {
            return result;
        }

        throw new BadRequestException(ErrorCodes.ValidationError, message, new { value });
    }

    private static string? ToJsonString(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : JsonSerializer.Serialize(value.Trim());
    }

    private static object? ResolveContractOldValue(RentalContract contract, string? fieldName)
    {
        return NormalizeFieldName(fieldName) switch
        {
            "startdate" => contract.StartDate,
            "enddate" => ContractAppendixAccessPolicy.GetCurrentContractEndDate(contract),
            "monthlyrent" => contract.MonthlyRent,
            "depositamount" => contract.DepositAmount,
            "paymentday" => contract.PaymentDay,
            "maintenantuserid" => ContractAppendixAccessPolicy.GetCurrentMainTenantUserId(contract),
            _ => null
        };
    }

    private static object? ResolveOccupantOldValue(RentalContract contract, Guid? occupantId, string? fieldName)
    {
        ContractOccupant? occupant = contract.Occupants.FirstOrDefault(x => x.Id == occupantId);
        if (occupant is null)
        {
            return null;
        }

        return NormalizeFieldName(fieldName) switch
        {
            "fullname" => occupant.FullName,
            "phonenumber" => occupant.PhoneNumber,
            "dateofbirth" => occupant.DateOfBirth,
            "relationshiptomaintenant" => occupant.RelationshipToMainTenant,
            "moveindate" => occupant.MoveInDate,
            "moveoutdate" => occupant.MoveOutDate,
            "status" => occupant.Status.ToString(),
            _ => null
        };
    }
}

internal sealed record ParsedAppendixChange(
    ContractAppendixChangeRequest Request,
    ContractAppendixChangeType ChangeType,
    ContractAppendixTargetType TargetType,
    int SortOrder);
