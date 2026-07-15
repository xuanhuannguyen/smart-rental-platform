using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class RentalContractStateGuard
{
    private const int LandlordSignatureMinimumStartOffsetDays = 2;

    public static void EnsureCanSubmitOccupants(RentalContract contract)
    {
        if (contract.Status is RentalContractStatus.WaitingTenantOccupants
            or RentalContractStatus.LandlordRevisionRequested)
        {
            return;
        }

        throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Trạng thái hợp đồng không cho phép cập nhật thông tin người ở.", new
        {
            Id = contract.Id,
            currentStatus = contract.Status.ToString()
        });
    }

    public static void EnsureCanLandlordSign(RentalContract contract)
    {
        if (contract.Status is RentalContractStatus.PendingLandlordSignature
            or RentalContractStatus.TenantRevisionRequested)
        {
            return;
        }

        throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Trạng thái hợp đồng không cho phép chủ trọ ký.", new
        {
            Id = contract.Id,
            currentStatus = contract.Status.ToString()
        });
    }

    public static void EnsureContractCanPreview(RentalContract contract)
    {
        if (contract.Status != RentalContractStatus.Active)
        {
            return;
        }

        throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Hợp đồng đã có hiệu lực, vui lòng xem file hợp đồng đã ký.", new
        {
            Id = contract.Id,
            currentStatus = contract.Status.ToString()
        });
    }

    public static void EnsureLandlordCanReject(RentalContract contract)
    {
        if (contract.Status is RentalContractStatus.PendingLandlordSignature
            or RentalContractStatus.TenantRevisionRequested)
        {
            return;
        }

        throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Trạng thái hợp đồng không cho phép chủ trọ từ chối.", new
        {
            Id = contract.Id,
            currentStatus = contract.Status.ToString()
        });
    }

    public static void EnsureCanView(Guid userId, RentalContract contract, Guid currentMainTenantUserId)
    {
        if (contract.Room.RoomingHouse.LandlordUserId == userId || currentMainTenantUserId == userId)
        {
            return;
        }

        throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Bạn không có quyền xem hợp đồng này.", new { contract.Id });
    }

    public static void EnsureMainTenant(Guid userId, RentalContract contract, Guid currentMainTenantUserId)
    {
        if (currentMainTenantUserId == userId)
        {
            return;
        }

        throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Bạn không có quyền thao tác trên hợp đồng này.", new { contract.Id });
    }

    public static void EnsureLandlord(Guid landlordUserId, RentalContract contract)
    {
        if (contract.Room.RoomingHouse.LandlordUserId == landlordUserId)
        {
            return;
        }

        throw new ForbiddenException("RENTAL_CONTRACT_FORBIDDEN", "Bạn không có quyền thao tác trên hợp đồng này.", new { contract.Id });
    }

    public static void EnsureStatus(RentalContract contract, RentalContractStatus expectedStatus)
    {
        if (contract.Status == expectedStatus)
        {
            return;
        }

        throw new ConflictException("RENTAL_CONTRACT_INVALID_STATUS", "Trạng thái hợp đồng không hợp lệ cho thao tác này.", new
        {
            Id = contract.Id,
            currentStatus = contract.Status.ToString(),
            expectedStatus = expectedStatus.ToString()
        });
    }

    public static void EnsureNotSigned(RentalContract contract, ContractSignerRole signerRole)
    {
        if (!contract.Signatures.Any(x => x.SignerRole == signerRole))
        {
            return;
        }

        throw new ConflictException("RENTAL_CONTRACT_ALREADY_SIGNED", "Bên này đã ký hợp đồng.", new
        {
            Id = contract.Id,
            signerRole = signerRole.ToString()
        });
    }

    public static void EnsureContractStartDateAllowsLandlordSignature(DateOnly startDate, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var minimumStartDate = today.AddDays(LandlordSignatureMinimumStartOffsetDays);
        if (startDate < minimumStartDate)
        {
            throw new BadRequestException(
                "VALIDATION_ERROR",
                "Ngày bắt đầu hợp đồng phải còn cách hôm nay ít nhất 2 ngày để người thuê có thời gian ký hợp đồng.");
        }
    }

    public static void EnsureTenantCanSignBeforeStartDate(DateOnly startDate, DateOnly today)
    {
        if (today > startDate)
        {
            throw new BadRequestException(
                "VALIDATION_ERROR",
                "Hợp đồng đã quá ngày bắt đầu thuê nên không thể ký.");
        }
    }

    public static void EnsureTenantSignatureDeadlineNotExpired(RentalContract contract, DateTimeOffset now)
    {
        if (contract.SignatureDeadlineAt.HasValue && contract.SignatureDeadlineAt.Value <= now)
        {
            throw new BadRequestException(
                "VALIDATION_ERROR",
                "Hợp đồng đã quá hạn ký. Vui lòng liên hệ chủ trọ để xử lý.");
        }
    }
}
