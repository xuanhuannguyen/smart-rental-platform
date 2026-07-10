using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.LegalDocuments.Requests;
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
    public async Task UpdateLegalDocumentAsync_ShouldLinkMediaAssetsAndReturnPrivateUrls()
    {
        var landlord = TestDataBuilder.BuildUser(email: "legal-owner@unit.test", displayName: "Legal Owner");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id, status: RoomingHouseApprovalStatus.Draft);
        var frontObjectKey = "public/rooming-house-legal-documents/front.jpg";
        var backObjectKey = "public/rooming-house-legal-documents/back.jpg";

        _fixture.Context.Users.Add(landlord);
        _fixture.Context.RoomingHouses.Add(house);
        _fixture.Context.MediaAssets.AddRange(
            new MediaAsset
            {
                Id = Guid.NewGuid(),
                OwnerUserId = landlord.Id,
                BucketName = "local-media",
                ObjectKey = frontObjectKey,
                OriginalFileName = "front.jpg",
                StoredFileName = "front.jpg",
                ContentType = "image/jpeg",
                FileSize = 11,
                Scope = MediaScope.RoomingHouseLegalDocument,
                Visibility = MediaVisibility.Public,
                Status = MediaStatus.Uploaded,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new MediaAsset
            {
                Id = Guid.NewGuid(),
                OwnerUserId = landlord.Id,
                BucketName = "local-media",
                ObjectKey = backObjectKey,
                OriginalFileName = "back.jpg",
                StoredFileName = "back.jpg",
                ContentType = "image/jpeg",
                FileSize = 12,
                Scope = MediaScope.RoomingHouseLegalDocument,
                Visibility = MediaVisibility.Public,
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
                FrontImageObjectKey = frontObjectKey,
                BackImageObjectKey = backObjectKey,
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
                    LegalDocument = house.LegalDocument is null
                        ? null
                        : new Contracts.LegalDocuments.Responses.RoomingHouseLegalDocumentResponse
                        {
                            RoomingHouseId = house.LegalDocument.RoomingHouseId,
                            FrontMediaAssetId = house.LegalDocument.FrontMediaAssetId,
                            BackMediaAssetId = house.LegalDocument.BackMediaAssetId,
                            ExtraMediaAssetId = house.LegalDocument.ExtraMediaAssetId,
                            DocumentType = house.LegalDocument.DocumentType.ToString(),
                            FrontImageObjectKey = house.LegalDocument.FrontImageObjectKey,
                            BackImageObjectKey = house.LegalDocument.BackImageObjectKey,
                            ExtraImageObjectKey = house.LegalDocument.ExtraImageObjectKey,
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
