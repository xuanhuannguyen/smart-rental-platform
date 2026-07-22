using System;
using System.Collections.Generic;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Contracts.RentalContracts.Requests;
using Xunit;

namespace SmartRentalPlatform.UnitTests.Contract;

public class RentalContractOccupantValidationTests
{
    [Fact]
    public void ValidateOccupantsRequest_ShouldThrowException_WhenMoveInDateIsBeforeStartDate()
    {
        // Arrange
        var request = new SubmitContractOccupantsRequest
        {
            Occupants = new List<ContractOccupantRequest>
            {
                new()
                {
                    Email = "tenant@test.com",
                    RelationshipToMainTenant = "Chủ hợp đồng",
                    MoveInDate = new DateOnly(2026, 7, 10), // Before start date
                    FullName = "Test Tenant",
                    PhoneNumber = "0123456789",
                    DateOfBirth = new DateOnly(1990, 1, 1)
                }
            }
        };

        var startDate = new DateOnly(2026, 7, 13);
        var endDate = new DateOnly(2027, 7, 13);

        // Act
        var exception = Assert.Throws<BadRequestException>(() =>
            RentalContractOccupantValidator.ValidateOccupantsRequest("tenant@test.com", request, 2, startDate, endDate));

        // Assert
        Assert.Equal("Ngày chuyển vào phải nằm trong khoảng thời gian có hiệu lực của hợp đồng.", exception.Message);
    }

    [Fact]
    public void ValidateOccupantsRequest_ShouldThrowException_WhenMoveInDateIsAfterEndDate()
    {
        // Arrange
        var request = new SubmitContractOccupantsRequest
        {
            Occupants = new List<ContractOccupantRequest>
            {
                new()
                {
                    Email = "tenant@test.com",
                    RelationshipToMainTenant = "Chủ hợp đồng",
                    MoveInDate = new DateOnly(2027, 7, 14), // After end date
                    FullName = "Test Tenant",
                    PhoneNumber = "0123456789",
                    DateOfBirth = new DateOnly(1990, 1, 1)
                }
            }
        };

        var startDate = new DateOnly(2026, 7, 13);
        var endDate = new DateOnly(2027, 7, 13);

        // Act
        var exception = Assert.Throws<BadRequestException>(() =>
            RentalContractOccupantValidator.ValidateOccupantsRequest("tenant@test.com", request, 2, startDate, endDate));

        // Assert
        Assert.Equal("Ngày chuyển vào phải nằm trong khoảng thời gian có hiệu lực của hợp đồng.", exception.Message);
    }

    [Fact]
    public void ValidateOccupantsRequest_ShouldNotThrowException_WhenMoveInDateIsWithinBounds()
    {
        // Arrange
        var request = new SubmitContractOccupantsRequest
        {
            Occupants = new List<ContractOccupantRequest>
            {
                new()
                {
                    Email = "tenant@test.com",
                    RelationshipToMainTenant = "Chủ hợp đồng",
                    MoveInDate = new DateOnly(2026, 7, 15), // Valid
                    FullName = "Test Tenant",
                    PhoneNumber = "0123456789",
                    DateOfBirth = new DateOnly(1990, 1, 1)
                }
            }
        };

        var startDate = new DateOnly(2026, 7, 13);
        var endDate = new DateOnly(2027, 7, 13);

        // Act & Assert
        RentalContractOccupantValidator.ValidateOccupantsRequest("tenant@test.com", request, 2, startDate, endDate); // Should not throw
    }
}
