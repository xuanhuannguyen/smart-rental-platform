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
