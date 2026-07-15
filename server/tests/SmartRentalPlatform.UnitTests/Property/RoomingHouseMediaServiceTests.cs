using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.LegalDocuments.Requests;
using SmartRentalPlatform.Contracts.PropertyImages.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses;
using SmartRentalPlatform.Contracts.RoomingHouses.Requests;
using SmartRentalPlatform.Contracts.RoomingHouses.Responses;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.Infrastructure.Persistence;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.Property;

public class RoomingHouseMediaServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();

    [Fact]
    public async Task UpdateImagesAsync_ShouldLinkSelectedMediaAssetsAndReturnMediaAssetIds()
    {
        var landlord = TestDataBuilder.BuildUser(email: "house-image-owner@unit.test", displayName: "House Image Owner");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Draft);
        var coverAssetId = Guid.NewGuid();
        var secondAssetId = Guid.NewGuid();
        var thirdAssetId = Guid.NewGuid();
        var imageObjectKey = "public/rooming-house-images/cover.jpg";

        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        _fixture.Context.MediaAssets.AddRange(
            BuildPublicImageAsset(coverAssetId, landlord.Id, imageObjectKey, MediaScope.RoomingHouseImage),
            BuildPublicImageAsset(secondAssetId, landlord.Id, "public/rooming-house-images/second.jpg", MediaScope.RoomingHouseImage),
            BuildPublicImageAsset(thirdAssetId, landlord.Id, "public/rooming-house-images/third.jpg", MediaScope.RoomingHouseImage));
        await _fixture.Context.SaveChangesAsync();

        var service = new RoomingHouseMediaService(
            _fixture.Context,
            new FakeRoomingHouseQueryService(_fixture.Context));

        var result = await service.UpdateImagesAsync(
            house.Id,
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
            .Where(x => x.RoomingHouseId == house.Id)
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
            Assert.Equal(MediaScope.RoomingHouseImage, asset.Scope);
            Assert.Equal(MediaVisibility.Public, asset.Visibility);
            Assert.Equal(MediaStatus.Linked, asset.Status);
        });
    }

    [Fact]
    public async Task UpdateImagesAsync_ShouldRejectMoreThanTenImages()
    {
        var landlord = TestDataBuilder.BuildUser(email: "house-image-limit@unit.test", displayName: "House Image Limit");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Draft);
        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        await _fixture.Context.SaveChangesAsync();

        var service = new RoomingHouseMediaService(
            _fixture.Context,
            new FakeRoomingHouseQueryService(_fixture.Context));
        var request = new UpdatePropertyImagesRequest
        {
            Images = Enumerable.Range(0, 11)
                .Select(index => new UpdatePropertyImageItemRequest
                {
                    MediaAssetId = Guid.NewGuid(),
                    IsCover = index == 0,
                    SortOrder = index
                })
                .ToList()
        };

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateImagesAsync(house.Id, request));

        Assert.Contains("tối đa 10 ảnh", exception.Message);
        Assert.Empty(_fixture.Context.PropertyImages);
    }

    [Fact]
    public async Task UpdateImagesAsync_ShouldResolveObjectKeysFromMediaAssetIds()
    {
        var landlord = TestDataBuilder.BuildUser(email: "house-image-media-id@unit.test", displayName: "House Image Media Id");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Draft);
        var coverAssetId = Guid.NewGuid();
        var secondAssetId = Guid.NewGuid();
        var thirdAssetId = Guid.NewGuid();

        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        _fixture.Context.MediaAssets.AddRange(
            BuildPublicImageAsset(coverAssetId, landlord.Id, "public/rooming-house-images/cover.jpg", MediaScope.RoomingHouseImage),
            BuildPublicImageAsset(secondAssetId, landlord.Id, "public/rooming-house-images/second.jpg", MediaScope.RoomingHouseImage),
            BuildPublicImageAsset(thirdAssetId, landlord.Id, "public/rooming-house-images/third.jpg", MediaScope.RoomingHouseImage));
        await _fixture.Context.SaveChangesAsync();

        var service = new RoomingHouseMediaService(
            _fixture.Context,
            new FakeRoomingHouseQueryService(_fixture.Context));

        var result = await service.UpdateImagesAsync(
            house.Id,
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
            .Where(x => x.RoomingHouseId == house.Id)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        Assert.All(imagesInDb, x => Assert.NotNull(x.MediaAssetId));
    }

    [Fact]
    public async Task UpdateImagesAsync_ShouldDeletePreviousMediaAssets_WhenReplacingImages()
    {
        var landlord = TestDataBuilder.BuildUser(email: "house-image-replace@unit.test", displayName: "House Image Replace");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Draft);
        var firstAssetIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var secondAssetIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        _fixture.Context.MediaAssets.AddRange(
            BuildPublicImageAsset(firstAssetIds[0], landlord.Id, "public/rooming-house-images/first-cover.jpg", MediaScope.RoomingHouseImage),
            BuildPublicImageAsset(firstAssetIds[1], landlord.Id, "public/rooming-house-images/first-second.jpg", MediaScope.RoomingHouseImage),
            BuildPublicImageAsset(firstAssetIds[2], landlord.Id, "public/rooming-house-images/first-third.jpg", MediaScope.RoomingHouseImage),
            BuildPublicImageAsset(secondAssetIds[0], landlord.Id, "public/rooming-house-images/second-cover.jpg", MediaScope.RoomingHouseImage),
            BuildPublicImageAsset(secondAssetIds[1], landlord.Id, "public/rooming-house-images/second-second.jpg", MediaScope.RoomingHouseImage),
            BuildPublicImageAsset(secondAssetIds[2], landlord.Id, "public/rooming-house-images/second-third.jpg", MediaScope.RoomingHouseImage));
        await _fixture.Context.SaveChangesAsync();

        var service = new RoomingHouseMediaService(
            _fixture.Context,
            new FakeRoomingHouseQueryService(_fixture.Context));

        await service.UpdateImagesAsync(
            house.Id,
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
            house.Id,
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

    [Fact]
    public async Task UpdateLegalDocumentAsync_ShouldLinkMediaAssetsAndReturnPrivateUrls()
    {
        var landlord = TestDataBuilder.BuildUser(email: "legal-owner@unit.test", displayName: "Legal Owner");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Draft);
        var frontAssetId = Guid.NewGuid();
        var backAssetId = Guid.NewGuid();
        var frontObjectKey = "private/rooming-house-legal-documents/front.jpg";
        var backObjectKey = "private/rooming-house-legal-documents/back.jpg";

        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        _fixture.Context.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = frontAssetId,
                OwnerUserId = landlord.Id,
                BucketName = "local-media",
                ObjectKey = frontObjectKey,
                OriginalFileName = "front.jpg",
                StoredFileName = "front.jpg",
                ContentType = "image/jpeg",
                FileSize = 11,
                Scope = MediaScope.RoomingHouseLegalDocument,
                Visibility = MediaVisibility.Private,
                Status = MediaStatus.Uploaded,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new MediaAsset
            {
                Id = backAssetId,
                OwnerUserId = landlord.Id,
                BucketName = "local-media",
                ObjectKey = backObjectKey,
                OriginalFileName = "back.jpg",
                StoredFileName = "back.jpg",
                ContentType = "image/jpeg",
                FileSize = 12,
                Scope = MediaScope.RoomingHouseLegalDocument,
                Visibility = MediaVisibility.Private,
                Status = MediaStatus.Uploaded,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        await _fixture.Context.SaveChangesAsync();

        var service = new RoomingHouseMediaService(
            _fixture.Context,
            new FakeRoomingHouseQueryService(_fixture.Context));

        var result = await service.UpdateLegalDocumentAsync(
            house.Id,
            new UpdateRoomingHouseLegalDocumentRequest
            {
                DocumentType = LegalDocumentType.LAND_USE_CERTIFICATE.ToString(),
                FrontMediaAssetId = frontAssetId,
                BackMediaAssetId = backAssetId,
                DocumentNumber = "123456789"
            });

        Assert.NotNull(result);
        Assert.NotNull(result!.LegalDocument);
        Assert.NotNull(result.LegalDocument.FrontMediaAssetId);
        Assert.NotNull(result.LegalDocument.BackMediaAssetId);
        Assert.Equal($"/api/media/private/{result.LegalDocument.FrontMediaAssetId:D}", result.LegalDocument.FrontImageUrl);

        var legalDocument = await _fixture.Context.RoomingHouseLegalDocuments.SingleAsync(x => x.RoomingHouseId == house.Id);
        Assert.NotNull(legalDocument.FrontMediaAssetId);
        Assert.NotNull(legalDocument.BackMediaAssetId);

        var linkedAssets = await _fixture.Context.MediaAssets
            .Where(x => x.LinkedEntityType == nameof(RoomingHouseLegalDocument) && x.LinkedEntityId == house.Id)
            .ToListAsync();

        Assert.Equal(2, linkedAssets.Count);
        Assert.All(linkedAssets, asset =>
        {
            Assert.Equal(MediaVisibility.Private, asset.Visibility);
            Assert.Equal(MediaStatus.Linked, asset.Status);
        });
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }

    private static MediaAsset BuildPublicImageAsset(
        Guid assetId,
        Guid ownerUserId,
        string objectKey,
        MediaScope scope)
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
            Scope = scope,
            Visibility = MediaVisibility.Public,
            Status = MediaStatus.Uploaded,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class FakeRoomingHouseQueryService : IRoomingHouseQueryService
    {
        private readonly AppDbContext _context;

        public FakeRoomingHouseQueryService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RoomingHouseDetailResponse?> GetByIdAsync(Guid roomingHouseId, CancellationToken cancellationToken = default)
        {
            var house = await _context.RoomingHouses
                .AsNoTracking()
                .Include(x => x.LegalDocument)
                .Include(x => x.Images)
                .Include(x => x.Rooms)
                .Include(x => x.RoomingHouseAmenities)
                    .ThenInclude(x => x.Amenity)
                .FirstOrDefaultAsync(x => x.Id == roomingHouseId, cancellationToken);

            return house is null
                ? null
                : new RoomingHouseDetailResponse
                {
                    Id = house.Id,
                    LandlordUserId = house.LandlordUserId,
                    Name = house.Name,
                    AddressLine = house.AddressLine,
                    ProvinceCode = house.ProvinceCode,
                    WardCode = house.WardCode,
                    AddressDisplay = house.AddressDisplay,
                    ApprovalStatus = house.ApprovalStatus.ToString(),
                    VisibilityStatus = house.VisibilityStatus.ToString(),
                    CreatedAt = house.CreatedAt,
                    UpdatedAt = house.UpdatedAt,
                    Images = house.Images
                        .OrderBy(x => x.SortOrder)
                        .Select(x => new Contracts.PropertyImages.Responses.PropertyImageResponse
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
                    LegalDocument = house.LegalDocument is null
                        ? null
                        : new Contracts.LegalDocuments.Responses.RoomingHouseLegalDocumentResponse
                        {
                            RoomingHouseId = house.LegalDocument.RoomingHouseId,
                            FrontMediaAssetId = house.LegalDocument.FrontMediaAssetId,
                            BackMediaAssetId = house.LegalDocument.BackMediaAssetId,
                            ExtraMediaAssetId = house.LegalDocument.ExtraMediaAssetId,
                            DocumentType = house.LegalDocument.DocumentType.ToString(),
                            FrontImageUrl = house.LegalDocument.FrontMediaAssetId.HasValue
                                ? $"/api/media/private/{house.LegalDocument.FrontMediaAssetId:D}"
                                : string.Empty,
                            BackImageUrl = house.LegalDocument.BackMediaAssetId.HasValue
                                ? $"/api/media/private/{house.LegalDocument.BackMediaAssetId:D}"
                                : string.Empty,
                            ExtraImageUrl = house.LegalDocument.ExtraMediaAssetId.HasValue
                                ? $"/api/media/private/{house.LegalDocument.ExtraMediaAssetId:D}"
                                : null,
                            DocumentNumberMasked = house.LegalDocument.DocumentNumberMasked,
                            UploadedAt = house.LegalDocument.UploadedAt,
                            CreatedAt = house.LegalDocument.CreatedAt,
                            UpdatedAt = house.LegalDocument.UpdatedAt
                        }
                };
        }

        public Task<RoomingHouseOnboardingResponse> GetOnboardingAsync(Guid landlordUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<RoomingHouseDetailResponse>> GetPublicAvailableAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<RoomingHouseListingResponse>> GetPublicListingAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<RoomingHouseSearchItemResponse>> SearchPublicAsync(RoomingHouseSearchRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RoomingHouseRecommendationResponse> GetGuestRecommendationsAsync(GuestRoomingHouseRecommendationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RoomingHouseDetailResponse?> GetPublicByIdAsync(Guid roomingHouseId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<RoomingHouseResponse>> GetByLandlordAsync(Guid landlordUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
