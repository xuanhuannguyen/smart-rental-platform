using System;
using System.Reflection;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Contract;

public class ContractAppendixOccupantValidationTests
{
    [Fact]
    public void ValidateAppendixOccupantRequest_ShouldThrowException_WhenMoveInDateIsBeforeEffectiveDate()
    {
        // Arrange
        var request = new ContractOccupantRequest
        {
            Email = "new_tenant@test.com",
            MoveInDate = new DateOnly(2026, 7, 10), // Before effective date
            FullName = "New Tenant",
            PhoneNumber = "0123456789",
            DateOfBirth = new DateOnly(1990, 1, 1)
        };

        var effectiveDate = new DateOnly(2026, 7, 13);
        var endDate = new DateOnly(2027, 7, 13);

        var methodInfo = typeof(ContractAppendixService).GetMethod("ValidateAppendixOccupantRequest", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var exception = Assert.Throws<TargetInvocationException>(() =>
            methodInfo!.Invoke(null, new object[] { request, effectiveDate, endDate }));

        // Assert
        var badRequestException = Assert.IsType<BadRequestException>(exception.InnerException);
        Assert.Equal("Ngày chuyển vào của người ở mới phải nằm trong khoảng thời gian hiệu lực của phụ lục và hợp đồng.", badRequestException.Message);
    }

    [Fact]
    public void ValidateAppendixOccupantRequest_ShouldThrowException_WhenMoveInDateIsAfterEndDate()
    {
        // Arrange
        var request = new ContractOccupantRequest
        {
            Email = "new_tenant@test.com",
            MoveInDate = new DateOnly(2027, 7, 14), // After end date
            FullName = "New Tenant",
            PhoneNumber = "0123456789",
            DateOfBirth = new DateOnly(1990, 1, 1)
        };

        var effectiveDate = new DateOnly(2026, 7, 13);
        var endDate = new DateOnly(2027, 7, 13);

        var methodInfo = typeof(ContractAppendixService).GetMethod("ValidateAppendixOccupantRequest", BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var exception = Assert.Throws<TargetInvocationException>(() =>
            methodInfo!.Invoke(null, new object[] { request, effectiveDate, endDate }));

        // Assert
        var badRequestException = Assert.IsType<BadRequestException>(exception.InnerException);
        Assert.Equal("Ngày chuyển vào của người ở mới phải nằm trong khoảng thời gian hiệu lực của phụ lục và hợp đồng.", badRequestException.Message);
    }

    [Fact]
    public void ValidateAppendixOccupantRequest_ShouldNotThrowException_WhenMoveInDateIsWithinBounds()
    {
        // Arrange
        var request = new ContractOccupantRequest
        {
            Email = "new_tenant@test.com",
            MoveInDate = new DateOnly(2026, 7, 15), // Valid
            FullName = "New Tenant",
            PhoneNumber = "0123456789",
            DateOfBirth = new DateOnly(1990, 1, 1)
        };

        var effectiveDate = new DateOnly(2026, 7, 13);
        var endDate = new DateOnly(2027, 7, 13);

        var methodInfo = typeof(ContractAppendixService).GetMethod("ValidateAppendixOccupantRequest", BindingFlags.NonPublic | BindingFlags.Static);

        // Act & Assert
        methodInfo!.Invoke(null, new object[] { request, effectiveDate, endDate }); // Should not throw
    }
}
