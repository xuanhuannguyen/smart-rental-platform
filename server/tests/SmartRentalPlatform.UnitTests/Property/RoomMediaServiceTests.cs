using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Media;
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
    public async Task UpdateImagesAsync_ShouldLinkSelectedMediaAssetsAndReturnMediaAssetIds()
    {
        var landlord = TestDataBuilder.BuildUser(email: "room-image-owner@unit.test", displayName: "Room Image Owner");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Approved);
        var room = TestDataBuilder.BuildRoom(house.Id, roomNumber: "101");
        var coverAssetId = Guid.NewGuid();
        var secondAssetId = Guid.NewGuid();
        var thirdAssetId = Guid.NewGuid();
        var imageObjectKey = "public/room-images/cover.jpg";

        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        _fixture.Context.Rooms.Add(room);
        _fixture.Context.MediaAssets.AddRange(
            BuildPublicImageAsset(coverAssetId, landlord.Id, imageObjectKey),
            BuildPublicImageAsset(secondAssetId, landlord.Id, "public/room-images/second.jpg"),
            BuildPublicImageAsset(thirdAssetId, landlord.Id, "public/room-images/third.jpg"));
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
                        MediaAssetId = coverAssetId,
                        IsCover = true,
                        SortOrder = 0
                    },
                    new UpdatePropertyImageItemRequest
                    {
                        MediaAssetId = secondAssetId,
                        IsCover = false,
                        SortOrder = 1
                    },
                    new UpdatePropertyImageItemRequest
                    {
                        MediaAssetId = thirdAssetId,
                        IsCover = false,
                        SortOrder = 2
                    }
                ]
            });

        Assert.NotNull(result);
        Assert.Equal(3, result!.Images.Count);
        Assert.Equal(
            [coverAssetId, secondAssetId, thirdAssetId],
            result.Images.OrderBy(x => x.SortOrder).Select(x => x.MediaAssetId).Cast<Guid>().ToArray());
        Assert.Equal($"/api/media/public/{coverAssetId:D}", result.Images[0].ImageUrl);

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

    [Fact]
    public async Task UpdateImagesAsync_ShouldResolveObjectKeysFromMediaAssetIds()
    {
        var landlord = TestDataBuilder.BuildUser(email: "room-image-media-id@unit.test", displayName: "Room Image Media Id");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Approved);
        var room = TestDataBuilder.BuildRoom(house.Id, roomNumber: "101");
        var coverAssetId = Guid.NewGuid();
        var secondAssetId = Guid.NewGuid();
        var thirdAssetId = Guid.NewGuid();

        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        _fixture.Context.Rooms.Add(room);
        _fixture.Context.MediaAssets.AddRange(
            BuildPublicImageAsset(coverAssetId, landlord.Id, "public/room-images/cover.jpg"),
            BuildPublicImageAsset(secondAssetId, landlord.Id, "public/room-images/second.jpg"),
            BuildPublicImageAsset(thirdAssetId, landlord.Id, "public/room-images/third.jpg"));
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
                        MediaAssetId = coverAssetId,
                        IsCover = true,
                        SortOrder = 0
                    },
                    new UpdatePropertyImageItemRequest
                    {
                        MediaAssetId = secondAssetId,
                        IsCover = false,
                        SortOrder = 1
                    },
                    new UpdatePropertyImageItemRequest
                    {
                        MediaAssetId = thirdAssetId,
                        IsCover = false,
                        SortOrder = 2
                    }
                ]
            });

        Assert.NotNull(result);
        Assert.Equal(
            [
                $"/api/media/public/{coverAssetId:D}",
                $"/api/media/public/{secondAssetId:D}",
                $"/api/media/public/{thirdAssetId:D}"
            ],
            result!.Images.OrderBy(x => x.SortOrder).Select(x => x.ImageUrl).ToArray());

        var imagesInDb = await _fixture.Context.PropertyImages
            .Where(x => x.RoomId == room.Id)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        Assert.All(imagesInDb, x => Assert.NotNull(x.MediaAssetId));
    }

    [Fact]
    public async Task UpdateImagesAsync_ShouldDeletePreviousMediaAssets_WhenReplacingImages()
    {
        var landlord = TestDataBuilder.BuildUser(email: "room-image-replace@unit.test", displayName: "Room Image Replace");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Approved);
        var room = TestDataBuilder.BuildRoom(house.Id, roomNumber: "101");
        var firstAssetIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var secondAssetIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        _fixture.Context.Rooms.Add(room);
        _fixture.Context.MediaAssets.AddRange(
            BuildPublicImageAsset(firstAssetIds[0], landlord.Id, "public/room-images/first-cover.jpg"),
            BuildPublicImageAsset(firstAssetIds[1], landlord.Id, "public/room-images/first-second.jpg"),
            BuildPublicImageAsset(firstAssetIds[2], landlord.Id, "public/room-images/first-third.jpg"),
            BuildPublicImageAsset(secondAssetIds[0], landlord.Id, "public/room-images/second-cover.jpg"),
            BuildPublicImageAsset(secondAssetIds[1], landlord.Id, "public/room-images/second-second.jpg"),
            BuildPublicImageAsset(secondAssetIds[2], landlord.Id, "public/room-images/second-third.jpg"));
        await _fixture.Context.SaveChangesAsync();

        var service = new RoomMediaService(
            _fixture.Context,
            new RoomAccessService(_fixture.Context),
            new FakeRoomQueryService(_fixture.Context, landlord.Id));

        await service.UpdateImagesAsync(
            landlord.Id,
            room.Id,
            new UpdatePropertyImagesRequest
            {
                Images =
                [
                    new UpdatePropertyImageItemRequest { MediaAssetId = firstAssetIds[0], IsCover = true, SortOrder = 0 },
                    new UpdatePropertyImageItemRequest { MediaAssetId = firstAssetIds[1], IsCover = false, SortOrder = 1 },
                    new UpdatePropertyImageItemRequest { MediaAssetId = firstAssetIds[2], IsCover = false, SortOrder = 2 }
                ]
            });

        var result = await service.UpdateImagesAsync(
            landlord.Id,
            room.Id,
            new UpdatePropertyImagesRequest
            {
                Images =
                [
                    new UpdatePropertyImageItemRequest { MediaAssetId = secondAssetIds[0], IsCover = true, SortOrder = 0 },
                    new UpdatePropertyImageItemRequest { MediaAssetId = secondAssetIds[1], IsCover = false, SortOrder = 1 },
                    new UpdatePropertyImageItemRequest { MediaAssetId = secondAssetIds[2], IsCover = false, SortOrder = 2 }
                ]
            });

        Assert.NotNull(result);
        Assert.Equal(secondAssetIds, result!.Images.OrderBy(x => x.SortOrder).Select(x => x.MediaAssetId).Cast<Guid>().ToArray());

        var retiredAssets = await _fixture.Context.MediaAssets
            .Where(x => firstAssetIds.Contains(x.Id))
            .ToListAsync();

        Assert.Equal(3, retiredAssets.Count);
        Assert.All(retiredAssets, asset =>
        {
            Assert.Equal(MediaStatus.Deleted, asset.Status);
            Assert.NotNull(asset.DeletedAt);
            Assert.Null(asset.LinkedEntityType);
            Assert.Null(asset.LinkedEntityId);
        });
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private static MediaAsset BuildPublicImageAsset(Guid assetId, Guid ownerUserId, string objectKey)
    {
        return new MediaAsset
        {
            Id = assetId,
            OwnerUserId = ownerUserId,
            BucketName = "local-media",
            ObjectKey = objectKey,
            OriginalFileName = Path.GetFileName(objectKey),
            StoredFileName = Path.GetFileName(objectKey),
            ContentType = "image/jpeg",
            FileSize = 11,
            Scope = MediaScope.RoomImage,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
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
                            ImageUrl = x.MediaAssetId.HasValue
                                ? PublicMediaPathBuilder.Build(x.MediaAssetId.Value)
                                : x.ImageUrl,
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
