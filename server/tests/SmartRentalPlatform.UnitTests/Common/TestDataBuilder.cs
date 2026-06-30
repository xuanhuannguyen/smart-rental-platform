using System;
using System.Collections.Generic;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Rental;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Entities.Billing;
using SmartRentalPlatform.Domain.Entities.Payments;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Domain.Enums.Rental;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Billing;
using SmartRentalPlatform.Domain.Enums.Payments;
using BillingInvoice = SmartRentalPlatform.Domain.Entities.Billing.Invoice;

namespace SmartRentalPlatform.UnitTests.Common;

public static class TestDataBuilder
{
    public static User BuildUser(Guid? id = null, string email = "test@user.com", string displayName = "Test User", UserStatus status = UserStatus.Active)
    {
        var now = DateTimeOffset.UtcNow;

        return new User
        {
            Id = id ?? Guid.NewGuid(),
            Email = email,
            NormalizedEmail = email.Trim().ToUpperInvariant(),
            DisplayName = displayName,
            Status = status,
            PasswordHash = "hashedpassword",
            OnboardingStatus = OnboardingStatus.Completed,
            EmailConfirmed = true,
            PhoneConfirmed = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static RoomingHouse BuildRoomingHouse(Guid landlordId, Guid? id = null, string name = "Mock House", RoomingHouseApprovalStatus status = RoomingHouseApprovalStatus.Approved)
    {
        return new RoomingHouse
        {
            Id = id ?? Guid.NewGuid(),
            LandlordUserId = landlordId,
            Name = name,
            AddressLine = "123 Street",
            WardCode = "W1",
            ProvinceCode = "P1",
            AddressDisplay = "123 Street, W1, P1",
            ApprovalStatus = status,
            VisibilityStatus = RoomingHouseVisibilityStatus.Visible,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static Room BuildRoom(Guid houseId, Guid? id = null, string roomNumber = "101", RoomStatus status = RoomStatus.Available)
    {
        return new Room
        {
            Id = id ?? Guid.NewGuid(),
            RoomingHouseId = houseId,
            RoomNumber = roomNumber,
            Floor = 1,
            AreaM2 = 25,
            MaxOccupants = 2,
            IsTieredPricing = false,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static RentalPolicy BuildRentalPolicy(Guid houseId, int depositMonths = 2)
    {
        return new RentalPolicy
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = houseId,
            DepositMonths = depositMonths,
            MinRentalMonths = 6,
            MaxRentalMonths = 12,
            DefaultPaymentDay = 5,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public static RentalRequest BuildRentalRequest(Guid tenantId, Guid roomId, Guid? id = null, RentalRequestStatus status = RentalRequestStatus.Pending)
    {
        return new RentalRequest
        {
            Id = id ?? Guid.NewGuid(),
            RoomId = roomId,
            TenantUserId = tenantId,
            DesiredStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            ExpectedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5).AddMonths(6)),
            ExpectedOccupantCount = 1,
            MonthlyRentSnapshot = 3000000,
            DepositAmountSnapshot = 6000000,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static RoomDeposit BuildRoomDeposit(Guid requestId, Guid roomId, Guid tenantId, Guid landlordId, Guid? id = null, RoomDepositStatus status = RoomDepositStatus.PendingPayment)
    {
        return new RoomDeposit
        {
            Id = id ?? Guid.NewGuid(),
            RentalRequestId = requestId,
            RoomId = roomId,
            TenantUserId = tenantId,
            LandlordUserId = landlordId,
            DepositAmount = 6000000,
            Currency = "VND",
            Status = status,
            PaymentDeadlineAt = DateTimeOffset.UtcNow.AddDays(2),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static RentalContract BuildRentalContract(Guid requestId, Guid roomId, Guid tenantId, Guid? id = null, RentalContractStatus status = RentalContractStatus.Active)
    {
        return new RentalContract
        {
            Id = id ?? Guid.NewGuid(),
            RentalRequestId = requestId,
            RoomId = roomId,
            MainTenantUserId = tenantId,
            ContractNumber = $"HD-TEST-{Guid.NewGuid():N}"[..20],
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            MonthlyRent = 3000000,
            DepositAmount = 6000000,
            PaymentDay = 5,
            Status = status,
            RoomSnapshot = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static WalletAccount BuildWalletAccount(Guid userId, decimal balance = 10000000)
    {
        return new WalletAccount
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Balance = balance,
            Currency = "VND",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static BillingInvoice BuildInvoice(Guid contractId, Guid roomId, Guid tenantId, Guid landlordId, Guid? id = null, InvoiceStatus status = InvoiceStatus.Issued)
    {
        return new BillingInvoice
        {
            Id = id ?? Guid.NewGuid(),
            ContractId = contractId,
            RoomId = roomId,
            TenantUserId = tenantId,
            LandlordUserId = landlordId,
            InvoiceNo = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20],
            BillingPeriodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            BillingPeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            RentAmount = 3000000,
            DiscountAmount = 0,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
