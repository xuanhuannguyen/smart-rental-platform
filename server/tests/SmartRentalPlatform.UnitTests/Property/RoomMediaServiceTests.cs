using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Rooms;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.PropertyImages.Requests;
using SmartRentalPlatform.Contracts.PropertyImages.Responses;
using SmartRentalPlatform.Contracts.RoomPriceTiers.Responses;
using SmartRentalPlatform.Contracts.Rooms.Responses;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Property;

public class RoomMediaServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();

    [Fact]
    public async Task UpdateImagesAsync_ShouldLinkPublicMediaAssetsAndReturnMediaAssetIds()
    {
        var landlord = TestDataBuilder.BuildUser(email: "room-image-owner@unit.test", displayName: "Room Image Owner");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Approved);
        var room = TestDataBuilder.BuildRoom(house.Id, roomNumber: "101");
        var imageObjectKey = "public/room-images/cover.jpg";

        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        _fixture.Context.Rooms.Add(room);
        _fixture.Context.MediaAssets.Add(new MediaAsset
        {
            Id = Guid.NewGuid(),
            OwnerUserId = landlord.Id,
            BucketName = "local-media",
            ObjectKey = imageObjectKey,
            OriginalFileName = "cover.jpg",
            StoredFileName = "cover.jpg",
            ContentType = "image/jpeg",
            FileSize = 11,
            Scope = MediaScope.RoomImage,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _fixture.Context.SaveChangesAsync();

        var service = new RoomMediaService(
            _fixture.Context,
            new RoomAccessService(_fixture.Context),
            new FakeRoomQueryService(_fixture.Context, landlord.Id));

        var result = await service.UpdateImagesAsync(
            landlord.Id,
            room.Id,
            new UpdatePropertyImagesRequest
            {
                Images =
                [
                    new UpdatePropertyImageItemRequest
                    {
                        ObjectKey = imageObjectKey,
                        IsCover = true,
                        SortOrder = 0
                    },
                    new UpdatePropertyImageItemRequest
                    {
                        ObjectKey = "public/room-images/second.jpg",
                        IsCover = false,
                        SortOrder = 1
                    },
                    new UpdatePropertyImageItemRequest
                    {
                        ObjectKey = "public/room-images/third.jpg",
                        IsCover = false,
                        SortOrder = 2
                    }
                ]
            });

        Assert.NotNull(result);
        Assert.Equal(3, result!.Images.Count);
        Assert.All(result.Images, image => Assert.NotNull(image.MediaAssetId));
        Assert.Equal($"/api/media/public/{imageObjectKey}", result.Images[0].ImageUrl);

        var imagesInDb = await _fixture.Context.PropertyImages
            .Where(x => x.RoomId == room.Id)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        Assert.Equal(3, imagesInDb.Count);
        Assert.All(imagesInDb, image => Assert.NotNull(image.MediaAssetId));

        var linkedAssets = await _fixture.Context.MediaAssets
            .Where(x => x.LinkedEntityType == nameof(PropertyImage))
            .ToListAsync();

        Assert.Equal(3, linkedAssets.Count);
        Assert.All(linkedAssets, asset =>
        {
            Assert.Equal(MediaScope.RoomImage, asset.Scope);
            Assert.Equal(MediaVisibility.Public, asset.Visibility);
            Assert.Equal(MediaStatus.Linked, asset.Status);
        });
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private sealed class FakeRoomQueryService : IRoomQueryService
    {
        private readonly AppDbContext _context;
        private readonly Guid _landlordUserId;

        public FakeRoomQueryService(AppDbContext context, Guid landlordUserId)
        {
            _context = context;
            _landlordUserId = landlordUserId;
        }

        public Task<List<RoomResponse>> GetByRoomingHouseAsync(Guid landlordUserId, Guid roomingHouseId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public async Task<RoomResponse?> GetByIdAsync(Guid landlordUserId, Guid roomId, CancellationToken cancellationToken = default)
        {
            if (landlordUserId != _landlordUserId)
            {
                return null;
            }

            var room = await _context.Rooms
                .AsNoTracking()
                .Include(x => x.Images)
                .Include(x => x.RoomAmenities)
                    .ThenInclude(x => x.Amenity)
                .Include(x => x.PriceTiers)
                .FirstOrDefaultAsync(x => x.Id == roomId, cancellationToken);

            return room is null
                ? null
                : new RoomResponse
                {
                    Id = room.Id,
                    RoomingHouseId = room.RoomingHouseId,
                    RoomNumber = room.RoomNumber,
                    Floor = room.Floor,
                    AreaM2 = room.AreaM2,
                    MaxOccupants = room.MaxOccupants,
                    IsTieredPricing = room.IsTieredPricing,
                    Status = room.Status.ToString(),
                    Description = room.Description,
                    CreatedAt = room.CreatedAt,
                    UpdatedAt = room.UpdatedAt,
                    Images = room.Images
                        .OrderBy(x => x.SortOrder)
                        .Select(x => new PropertyImageResponse
                        {
                            Id = x.Id,
                            MediaAssetId = x.MediaAssetId,
                            ObjectKey = x.ObjectKey,
                            ImageUrl = $"/api/media/public/{x.ObjectKey}",
                            Caption = x.Caption,
                            IsCover = x.IsCover,
                            SortOrder = x.SortOrder,
                            CreatedAt = x.CreatedAt
                        })
                        .ToList(),
                    Amenities = room.RoomAmenities
                        .Select(x => new AmenityResponse
                        {
                            Id = x.AmenityId,
                            Name = x.Amenity.Name,
                            Scope = x.Amenity.Scope.ToString(),
                            IconCode = x.Amenity.IconCode
                        })
                        .ToList(),
                    PriceTiers = room.PriceTiers
                        .OrderBy(x => x.OccupantCount)
                        .Select(x => new RoomPriceTierResponse
                        {
                            Id = x.Id,
                            OccupantCount = x.OccupantCount,
                            MonthlyRent = x.MonthlyRent,
                            IsActive = x.IsActive
                        })
                        .ToList()
                };
        }

        public Task<RoomResponse?> GetPublicRoomByIdAsync(Guid roomId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<List<RoomResponse>> GetPublicAvailableRoomsAsync(Guid roomingHouseId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
