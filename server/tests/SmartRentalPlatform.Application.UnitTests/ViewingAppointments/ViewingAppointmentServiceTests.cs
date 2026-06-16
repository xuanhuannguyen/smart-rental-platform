using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.ViewingAppointments;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.ViewingAppointments.Requests;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Infrastructure.Persistence;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SmartRentalPlatform.Application.UnitTests.ViewingAppointments
{
    public class ViewingAppointmentServiceTests
    {
        private AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new AppDbContext(options);
        }

        private async Task<(Guid LandlordId, Guid TenantId, Guid RoomId)> SeedBaseDataAsync(AppDbContext context, RoomStatus roomStatus = RoomStatus.Available, RoomingHouseApprovalStatus houseApproval = RoomingHouseApprovalStatus.Approved, RoomingHouseVisibilityStatus houseVisibility = RoomingHouseVisibilityStatus.Visible)
        {
            var landlord = new User
            {
                Id = Guid.NewGuid(),
                Email = "landlord@test.com",
                NormalizedEmail = "LANDLORD@TEST.COM",
                DisplayName = "Landlord Test",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var tenant = new User
            {
                Id = Guid.NewGuid(),
                Email = "tenant@test.com",
                NormalizedEmail = "TENANT@TEST.COM",
                DisplayName = "Tenant Test",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var house = new RoomingHouse
            {
                Id = Guid.NewGuid(),
                LandlordUserId = landlord.Id,
                Name = "Khu tro test",
                AddressLine = "123 Test St",
                AddressDisplay = "123 Test St, Ward, Province",
                ApprovalStatus = houseApproval,
                VisibilityStatus = houseVisibility,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var room = new Room
            {
                Id = Guid.NewGuid(),
                RoomingHouseId = house.Id,
                RoomNumber = "101",
                Status = roomStatus,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            context.Users.AddRange(landlord, tenant);
            context.RoomingHouses.Add(house);
            context.Rooms.Add(room);
            await context.SaveChangesAsync();

            return (landlord.Id, tenant.Id, room.Id);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenTimeInPast()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var request = new CreateViewingAppointmentRequest
            {
                RoomId = roomId,
                ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                DurationMinutes = 30
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                service.CreateAsync(tenantId, request, CancellationToken.None));
            Assert.Equal(ErrorCodes.ViewingAppointmentTimeInPast, ex.ErrorCode);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenRoomNotFound()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var request = new CreateViewingAppointmentRequest
            {
                RoomId = Guid.NewGuid(), // Invalid Room ID
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(1),
                DurationMinutes = 30
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
                service.CreateAsync(tenantId, request, CancellationToken.None));
            Assert.Equal(ErrorCodes.RoomNotFound, ex.ErrorCode);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenRoomNotAvailable()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context, roomStatus: RoomStatus.Occupied);

            var request = new CreateViewingAppointmentRequest
            {
                RoomId = roomId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(1),
                DurationMinutes = 30
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                service.CreateAsync(tenantId, request, CancellationToken.None));
            Assert.Equal(ErrorCodes.RoomNotAvailable, ex.ErrorCode);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenHouseNotApproved()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context, houseApproval: RoomingHouseApprovalStatus.Pending);

            var request = new CreateViewingAppointmentRequest
            {
                RoomId = roomId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(1),
                DurationMinutes = 30
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                service.CreateAsync(tenantId, request, CancellationToken.None));
            Assert.Equal(ErrorCodes.HouseNotApproved, ex.ErrorCode);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenHouseHidden()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context, houseVisibility: RoomingHouseVisibilityStatus.Hidden);

            var request = new CreateViewingAppointmentRequest
            {
                RoomId = roomId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(1),
                DurationMinutes = 30
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                service.CreateAsync(tenantId, request, CancellationToken.None));
            Assert.Equal(ErrorCodes.HouseNotPublic, ex.ErrorCode);
        }

        [Fact]
        public async Task CreateAsync_ShouldSucceed_WhenValid()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var request = new CreateViewingAppointmentRequest
            {
                RoomId = roomId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(2),
                DurationMinutes = 45,
                TenantNote = "Toi muon xem phong rong rai"
            };

            // Act
            var result = await service.CreateAsync(tenantId, request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(roomId, result.RoomId);
            Assert.Equal(tenantId, result.TenantUserId);
            Assert.Equal("Pending", result.Status);
            Assert.Equal(45, result.DurationMinutes);
            Assert.Equal("Toi muon xem phong rong rai", result.TenantNote);

            var dbAppointment = await context.ViewingAppointments.FirstOrDefaultAsync(x => x.Id == result.Id);
            Assert.NotNull(dbAppointment);
            Assert.Equal(ViewingAppointmentStatus.Pending, dbAppointment.Status);
        }

        [Fact]
        public async Task ConfirmAsync_ShouldThrow_WhenConflictAndBypassFalse()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var baseTime = DateTimeOffset.UtcNow.AddHours(5);

            // Seed an existing Confirmed appointment [10:00 - 10:30]
            var existingConfirmed = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = baseTime, // 10:00
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Confirmed
            };
            context.ViewingAppointments.Add(existingConfirmed);

            // New Pending appointment [10:15 - 10:45] (starts inside existing)
            var pendingAppt = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = baseTime.AddMinutes(15), // 10:15
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };
            context.ViewingAppointments.Add(pendingAppt);
            await context.SaveChangesAsync();

            var request = new ConfirmViewingAppointmentRequest
            {
                ConfirmDespiteConflict = false,
                LandlordNote = "Conflict confirm test"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                service.ConfirmAsync(landlordId, pendingAppt.Id, request, CancellationToken.None));
            Assert.Equal(ErrorCodes.ViewingAppointmentConflict, ex.ErrorCode);
        }

        [Fact]
        public async Task ConfirmAsync_ShouldSucceed_WhenConflictAndBypassTrue()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var baseTime = DateTimeOffset.UtcNow.AddHours(5);

            // Seed an existing Confirmed appointment [10:00 - 10:30]
            var existingConfirmed = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = baseTime,
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Confirmed
            };
            context.ViewingAppointments.Add(existingConfirmed);

            // New Pending appointment [10:15 - 10:45]
            var pendingAppt = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = baseTime.AddMinutes(15),
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };
            context.ViewingAppointments.Add(pendingAppt);
            await context.SaveChangesAsync();

            var request = new ConfirmViewingAppointmentRequest
            {
                ConfirmDespiteConflict = true, // Force confirm
                LandlordNote = "Bypass conflict"
            };

            // Act
            var result = await service.ConfirmAsync(landlordId, pendingAppt.Id, request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Confirmed", result.Status);
            Assert.Equal("Bypass conflict", result.LandlordNote);

            var dbAppt = await context.ViewingAppointments.FirstOrDefaultAsync(x => x.Id == pendingAppt.Id);
            Assert.Equal(ViewingAppointmentStatus.Confirmed, dbAppt!.Status);
        }

        [Fact]
        public async Task Overlap_EdgeCases_Tests()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var baseTime = DateTimeOffset.UtcNow.AddHours(10); // Start boundary: 10:00

            // Confirmed Appt 1: [10:00 - 10:30]
            var existing = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = baseTime,
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Confirmed
            };
            context.ViewingAppointments.Add(existing);

            // Test case 1: Ends exactly when existing starts [09:30 - 10:00] (No overlap)
            var appt1 = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = baseTime.AddMinutes(-30),
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };
            // Test case 2: Starts exactly when existing ends [10:30 - 11:00] (No overlap)
            var appt2 = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = baseTime.AddMinutes(30),
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };
            // Test case 3: Overlaps slightly at the start [09:45 - 10:15]
            var appt3 = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = baseTime.AddMinutes(-15),
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };

            context.ViewingAppointments.AddRange(appt1, appt2, appt3);
            await context.SaveChangesAsync();

            // Act
            var check1 = await service.CheckConflictAsync(landlordId, appt1.Id);
            var check2 = await service.CheckConflictAsync(landlordId, appt2.Id);
            var check3 = await service.CheckConflictAsync(landlordId, appt3.Id);

            // Assert
            Assert.False(check1.HasConflict);
            Assert.False(check2.HasConflict);
            Assert.True(check3.HasConflict);
            Assert.Single(check3.ConflictingAppointments);
            Assert.Equal(existing.Id, check3.ConflictingAppointments[0].Id);
        }

        [Fact]
        public async Task RejectAsync_ShouldThrow_WhenReasonMissing()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var pending = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(2),
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };
            context.ViewingAppointments.Add(pending);
            await context.SaveChangesAsync();

            var request = new RejectViewingAppointmentRequest
            {
                RejectReason = "" // Missing reason
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                service.RejectAsync(landlordId, pending.Id, request, CancellationToken.None));
            Assert.Equal(ErrorCodes.ViewingAppointmentReasonRequired, ex.ErrorCode);
        }

        [Fact]
        public async Task RejectAsync_ShouldSucceed_WhenValid()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var pending = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(2),
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };
            context.ViewingAppointments.Add(pending);
            await context.SaveChangesAsync();

            var request = new RejectViewingAppointmentRequest
            {
                RejectReason = "Ly do tu choi hop le"
            };

            // Act
            var result = await service.RejectAsync(landlordId, pending.Id, request, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Rejected", result.Status);
            Assert.Equal("Ly do tu choi hop le", result.CancelReason);

            var dbAppt = await context.ViewingAppointments.FirstOrDefaultAsync(x => x.Id == pending.Id);
            Assert.Equal(ViewingAppointmentStatus.Rejected, dbAppt!.Status);
            Assert.Equal("Ly do tu choi hop le", dbAppt.CancelReason);
        }

        [Fact]
        public async Task CompleteAsync_ShouldThrow_WhenTimeNotReached()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var futureConfirmed = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(2), // Future
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Confirmed
            };
            context.ViewingAppointments.Add(futureConfirmed);
            await context.SaveChangesAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                service.CompleteAsync(landlordId, futureConfirmed.Id, CancellationToken.None));
            Assert.Equal(ErrorCodes.ViewingAppointmentNotAllowed, ex.ErrorCode);
        }

        [Fact]
        public async Task CompleteAsync_ShouldSucceed_WhenTimeReached()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var pastConfirmed = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(-1), // Past
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Confirmed
            };
            context.ViewingAppointments.Add(pastConfirmed);
            await context.SaveChangesAsync();

            // Act
            var result = await service.CompleteAsync(landlordId, pastConfirmed.Id, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Completed", result.Status);

            var dbAppt = await context.ViewingAppointments.FirstOrDefaultAsync(x => x.Id == pastConfirmed.Id);
            Assert.Equal(ViewingAppointmentStatus.Completed, dbAppt!.Status);
        }

        [Fact]
        public async Task CheckConflictAsync_ShouldDetectConflict_AcrossDifferentRoomsForSameLandlord()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId1) = await SeedBaseDataAsync(context);

            // Create a second room in a different house for the same landlord
            var house2 = new RoomingHouse
            {
                Id = Guid.NewGuid(),
                LandlordUserId = landlordId,
                Name = "Khu tro test 2",
                AddressLine = "456 Test St",
                AddressDisplay = "456 Test St, Ward, Province",
                ApprovalStatus = RoomingHouseApprovalStatus.Approved,
                VisibilityStatus = RoomingHouseVisibilityStatus.Visible,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            var roomId2 = Guid.NewGuid();
            var room2 = new Room
            {
                Id = roomId2,
                RoomingHouseId = house2.Id,
                RoomNumber = "202",
                Status = RoomStatus.Available,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.RoomingHouses.Add(house2);
            context.Rooms.Add(room2);

            var baseTime = DateTimeOffset.UtcNow.AddHours(5);

            // Confirmed appointment on Room 1: [10:00 - 10:30]
            var apptConfirmed = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId1,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = baseTime,
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Confirmed
            };

            // Pending appointment on Room 2: [10:15 - 10:45] (overlaps with Appt on Room 1)
            var apptPending = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId2,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = baseTime.AddMinutes(15),
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };

            context.ViewingAppointments.AddRange(apptConfirmed, apptPending);
            await context.SaveChangesAsync();

            // Act
            var check = await service.CheckConflictAsync(landlordId, apptPending.Id, CancellationToken.None);

            // Assert
            Assert.True(check.HasConflict);
            Assert.Single(check.ConflictingAppointments);
            Assert.Equal(apptConfirmed.Id, check.ConflictingAppointments[0].Id);
        }

        [Fact]
        public async Task CheckConflictAsync_ShouldIgnoreNonConfirmedStatuses()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var baseTime = DateTimeOffset.UtcNow.AddHours(5);

            // Seed multiple overlapping appointments with non-confirmed statuses
            var statusesToTest = new[] 
            { 
                ViewingAppointmentStatus.Pending, 
                ViewingAppointmentStatus.Rejected, 
                ViewingAppointmentStatus.CancelledByTenant, 
                ViewingAppointmentStatus.CancelledByLandlord, 
                ViewingAppointmentStatus.Completed, 
                ViewingAppointmentStatus.Expired 
            };

            foreach (var status in statusesToTest)
            {
                context.ViewingAppointments.Add(new ViewingAppointment
                {
                    Id = Guid.NewGuid(),
                    RoomId = roomId,
                    TenantUserId = tenantId,
                    CreatedByUserId = tenantId,
                    ScheduledAt = baseTime,
                    DurationMinutes = 30,
                    Status = status
                });
            }

            var pendingAppt = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = baseTime.AddMinutes(10), // Overlaps
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };
            context.ViewingAppointments.Add(pendingAppt);
            await context.SaveChangesAsync();

            // Act
            var check = await service.CheckConflictAsync(landlordId, pendingAppt.Id, CancellationToken.None);

            // Assert
            Assert.False(check.HasConflict);
            Assert.Empty(check.ConflictingAppointments);
        }

        [Fact]
        public async Task CancelByTenantAsync_ShouldThrowNotFound_WhenCalledByAnotherTenant()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var pending = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(2),
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };
            context.ViewingAppointments.Add(pending);
            await context.SaveChangesAsync();

            var maliciousTenantId = Guid.NewGuid(); // Random user ID representing another tenant
            var request = new CancelViewingAppointmentRequest { CancelReason = "Hack attempt" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
                service.CancelByTenantAsync(maliciousTenantId, pending.Id, request, CancellationToken.None));
            Assert.Equal(ErrorCodes.ViewingAppointmentNotFound, ex.ErrorCode);
        }

        [Fact]
        public async Task ConfirmAsync_ShouldThrowNotFound_WhenCalledByAnotherLandlord()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var pending = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(2),
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };
            context.ViewingAppointments.Add(pending);
            await context.SaveChangesAsync();

            var otherLandlordId = Guid.NewGuid();
            var request = new ConfirmViewingAppointmentRequest { ConfirmDespiteConflict = false };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
                service.ConfirmAsync(otherLandlordId, pending.Id, request, CancellationToken.None));
            Assert.Equal(ErrorCodes.ViewingAppointmentNotFound, ex.ErrorCode);
        }

        [Fact]
        public async Task GetLandlordAppointmentsAsync_ShouldThrow_WhenStatusInvalid()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                service.GetLandlordAppointmentsAsync(landlordId, "INVALID_STATUS_XYZ", CancellationToken.None));
            Assert.Equal(ErrorCodes.InvalidStatus, ex.ErrorCode);
        }
        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenTenantIsLandlordOfRoom()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var request = new CreateViewingAppointmentRequest
            {
                RoomId = roomId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(3),
                DurationMinutes = 30
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                service.CreateAsync(landlordId, request, CancellationToken.None)); // Use landlordId as tenant
            Assert.Equal(ErrorCodes.ViewingAppointmentNotAllowed, ex.ErrorCode);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenDuplicateActiveExists()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            // Existing Pending
            var pendingAppt = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(5),
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };
            context.ViewingAppointments.Add(pendingAppt);
            await context.SaveChangesAsync();

            var request = new CreateViewingAppointmentRequest
            {
                RoomId = roomId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(6),
                DurationMinutes = 30
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                service.CreateAsync(tenantId, request, CancellationToken.None));
            Assert.Equal(ErrorCodes.ViewingAppointmentDuplicate, ex.ErrorCode);
        }

        [Fact]
        public async Task CreateAsync_ShouldThrow_WhenScheduledLessThan2HoursFromNow()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            var request = new CreateViewingAppointmentRequest
            {
                RoomId = roomId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(1), // Less than 2 hours
                DurationMinutes = 30
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                service.CreateAsync(tenantId, request, CancellationToken.None));
            Assert.Equal(ErrorCodes.ViewingAppointmentTimeInPast, ex.ErrorCode);
        }

        [Fact]
        public async Task GetMyAppointments_ShouldAutoExpire_PendingPastScheduledAt()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            // Existing Pending but past scheduled time
            var pastPendingAppt = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(-1), // Past
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };
            context.ViewingAppointments.Add(pastPendingAppt);
            await context.SaveChangesAsync();

            // Act
            var results = await service.GetMyAppointmentsAsync(tenantId, CancellationToken.None);

            // Assert
            Assert.Single(results);
            Assert.Equal("Expired", results[0].Status);

            var dbAppt = await context.ViewingAppointments.FirstOrDefaultAsync(x => x.Id == pastPendingAppt.Id);
            Assert.Equal(ViewingAppointmentStatus.Expired, dbAppt!.Status);
        }

        [Fact]
        public async Task GetLandlordAppointments_ShouldAutoExpire_PendingPastScheduledAt()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new ViewingAppointmentService(context);
            var (landlordId, tenantId, roomId) = await SeedBaseDataAsync(context);

            // Existing Pending but past scheduled time
            var pastPendingAppt = new ViewingAppointment
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                TenantUserId = tenantId,
                CreatedByUserId = tenantId,
                ScheduledAt = DateTimeOffset.UtcNow.AddHours(-1), // Past
                DurationMinutes = 30,
                Status = ViewingAppointmentStatus.Pending
            };
            context.ViewingAppointments.Add(pastPendingAppt);
            await context.SaveChangesAsync();

            // Act
            var results = await service.GetLandlordAppointmentsAsync(landlordId, null, CancellationToken.None);

            // Assert
            Assert.Single(results);
            Assert.Equal("Expired", results[0].Status);

            var dbAppt = await context.ViewingAppointments.FirstOrDefaultAsync(x => x.Id == pastPendingAppt.Id);
            Assert.Equal(ViewingAppointmentStatus.Expired, dbAppt!.Status);
        }
    }
}
