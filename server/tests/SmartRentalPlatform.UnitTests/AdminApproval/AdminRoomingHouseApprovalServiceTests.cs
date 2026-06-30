using SmartRentalPlatform.Application.AdminApproval;
using SmartRentalPlatform.Application.Users;
using SmartRentalPlatform.Contracts.Users;
using SmartRentalPlatform.Contracts.Users.Requests;
using SmartRentalPlatform.Contracts.Users.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.AdminApproval;

public class AdminRoomingHouseApprovalServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();
    private readonly FakeUserService _userService = new();

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPendingNonDeletedHouses()
    {
        var landlord = TestDataBuilder.BuildUser(email: "landlord-pending@unit.test", displayName: "Pending Landlord");
        var pending = TestDataBuilder.BuildRoomingHouse(landlord.Id, name: "Pending House", status: RoomingHouseApprovalStatus.Pending);
        pending.UpdatedAt = DateTimeOffset.UtcNow.AddDays(1);
        var approved = TestDataBuilder.BuildRoomingHouse(landlord.Id, name: "Approved House", status: RoomingHouseApprovalStatus.Approved);
        var deleted = TestDataBuilder.BuildRoomingHouse(landlord.Id, name: "Deleted House", status: RoomingHouseApprovalStatus.Pending);
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.AddRange(pending, approved, deleted);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService();

        var result = await service.GetPendingAsync(pageNumber: 1, pageSize: 10);

        Assert.Contains(result.Items, x => x.Id == pending.Id && x.LandlordEmail == landlord.Email);
        Assert.DoesNotContain(result.Items, x => x.Id == approved.Id || x.Id == deleted.Id);
    }

    [Fact]
    public async Task GetDetailAsync_WhenHouseExists_ReturnsLegalDocumentImagesAmenitiesAndRooms()
    {
        var landlord = TestDataBuilder.BuildUser(email: "detail-house@unit.test", displayName: "House Landlord");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, name: "Detail House", status: RoomingHouseApprovalStatus.Pending);
        var roomA = TestDataBuilder.BuildRoom(house.Id, roomNumber: "102");
        roomA.Floor = 2;
        var roomB = TestDataBuilder.BuildRoom(house.Id, roomNumber: "101");
        roomB.Floor = 1;
        var deletedRoom = TestDataBuilder.BuildRoom(house.Id, roomNumber: "999");
        deletedRoom.DeletedAt = DateTimeOffset.UtcNow;
        var amenity = new Amenity { Id = 90401, Name = "Unit Amenity", Scope = AmenityScope.House, IconCode = "unit", IsActive = true };
        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        _fixture.Context.Rooms.AddRange(roomA, roomB, deletedRoom);
        _fixture.Context.Amenities.Add(amenity);
        _fixture.Context.RoomingHouseAmenities.Add(new RoomingHouseAmenity { RoomingHouseId = house.Id, AmenityId = amenity.Id });
        _fixture.Context.PropertyImages.AddRange(
            new PropertyImage { Id = Guid.NewGuid(), RoomingHouseId = house.Id, ObjectKey = "second", ImageUrl = "/second.jpg", SortOrder = 2, CreatedAt = DateTimeOffset.UtcNow },
            new PropertyImage { Id = Guid.NewGuid(), RoomingHouseId = house.Id, ObjectKey = "first", ImageUrl = "/first.jpg", SortOrder = 1, CreatedAt = DateTimeOffset.UtcNow });
        _fixture.Context.RoomingHouseLegalDocuments.Add(new RoomingHouseLegalDocument
        {
            RoomingHouseId = house.Id,
            FrontImageObjectKey = "front",
            BackImageObjectKey = "back",
            DocumentNumberMasked = "123***",
            DocumentNumberHash = "hash",
            UploadedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService();

        var result = await service.GetDetailAsync(house.Id);

        Assert.NotNull(result);
        Assert.Equal(house.Id, result.Id);
        Assert.NotNull(result.LegalDocument);
        Assert.Equal(["first", "second"], result.Images.Select(x => x.ObjectKey));
        var mappedAmenity = Assert.Single(result.Amenities);
        Assert.Equal("Unit Amenity", mappedAmenity.Name);
        Assert.Equal([roomB.Id, roomA.Id], result.Rooms.Select(x => x.Id));
    }

    [Fact]
    public async Task GetDetailAsync_WhenHouseMissing_ReturnsNull()
    {
        var service = CreateService();

        var result = await service.GetDetailAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ApproveAsync_WhenHousePending_ApprovesHouseAndGrantsLandlordRole()
    {
        var adminId = Guid.NewGuid();
        var landlord = TestDataBuilder.BuildUser();
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Pending);
        house.VisibilityStatus = RoomingHouseVisibilityStatus.Hidden;
        house.RejectedReason = "old";
        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService();

        var result = await service.ApproveAsync(house.Id, adminId);

        Assert.True(result);
        Assert.Equal(RoomingHouseApprovalStatus.Approved, house.ApprovalStatus);
        Assert.Equal(RoomingHouseVisibilityStatus.Visible, house.VisibilityStatus);
        Assert.Null(house.RejectedReason);
        Assert.Equal(adminId, house.ReviewedByAdminId);
        Assert.Contains(house.Id, _userService.GrantedRoomingHouseIds);
    }

    [Fact]
    public async Task ApproveAsync_WhenHouseNotPending_ReturnsFalse()
    {
        var landlord = TestDataBuilder.BuildUser();
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Approved);
        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService();

        var result = await service.ApproveAsync(house.Id, Guid.NewGuid());

        Assert.False(result);
        Assert.Empty(_userService.GrantedRoomingHouseIds);
    }

    [Fact]
    public async Task RejectAsync_WhenReasonBlank_ReturnsFalse()
    {
        var service = CreateService();

        var result = await service.RejectAsync(Guid.NewGuid(), Guid.NewGuid(), "   ");

        Assert.False(result);
    }

    [Fact]
    public async Task RejectAsync_WhenHousePending_RejectsAndTrimsReason()
    {
        var adminId = Guid.NewGuid();
        var landlord = TestDataBuilder.BuildUser();
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Pending);
        house.VisibilityStatus = RoomingHouseVisibilityStatus.Visible;
        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService();

        var result = await service.RejectAsync(house.Id, adminId, "  missing legal document  ");

        Assert.True(result);
        Assert.Equal(RoomingHouseApprovalStatus.Rejected, house.ApprovalStatus);
        Assert.Equal(RoomingHouseVisibilityStatus.Hidden, house.VisibilityStatus);
        Assert.Equal("missing legal document", house.RejectedReason);
        Assert.Equal(adminId, house.ReviewedByAdminId);
    }

    [Fact]
    public async Task GetPublicAsync_ReturnsOnlyApprovedNonDeletedHouses()
    {
        var landlord = TestDataBuilder.BuildUser(email: "public-house@unit.test", displayName: "Public Landlord");
        var approved = TestDataBuilder.BuildRoomingHouse(landlord.Id, name: "Approved Public", status: RoomingHouseApprovalStatus.Approved);
        var pending = TestDataBuilder.BuildRoomingHouse(landlord.Id, name: "Pending Public", status: RoomingHouseApprovalStatus.Pending);
        var deleted = TestDataBuilder.BuildRoomingHouse(landlord.Id, name: "Deleted Public", status: RoomingHouseApprovalStatus.Approved);
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.AddRange(approved, pending, deleted);
        await _fixture.Context.SaveChangesAsync();

        var service = CreateService();

        var result = await service.GetPublicAsync(pageNumber: 1, pageSize: 10);

        Assert.Contains(result.Items, x => x.Id == approved.Id);
        Assert.DoesNotContain(result.Items, x => x.Id == pending.Id || x.Id == deleted.Id);
    }

    private AdminRoomingHouseApprovalService CreateService()
    {
        return new AdminRoomingHouseApprovalService(_fixture.Context, _userService);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private sealed class FakeUserService : IUserService
    {
        public List<Guid> GrantedRoomingHouseIds { get; } = new();

        public Task GrantLandlordRoleAfterRoomingHouseApprovedAsync(Guid roomingHouseId, CancellationToken cancellationToken = default)
        {
            GrantedRoomingHouseIds.Add(roomingHouseId);
            return Task.CompletedTask;
        }

        public Task AssignDefaultTenantRoleAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<UserSessionResponse>> GetActiveSessionsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LandlordEligibilityResponse> GetLandlordEligibilityAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserProfileResponse> GetUserProfileAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OccupantAccountLookupResponse> LookupOccupantAccountAsync(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserProfileResponse> UpdateUserProfileAsync(UpdateUserProfileRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
