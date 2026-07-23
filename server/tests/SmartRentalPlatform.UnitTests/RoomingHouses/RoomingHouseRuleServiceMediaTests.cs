using SmartRentalPlatform.Application.Common.Media;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Contracts.Files;
using SmartRentalPlatform.Contracts.Files.Responses;
using SmartRentalPlatform.Contracts.RoomingHouseRules.Requests;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.Properties;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.RoomingHouses;

public sealed class RoomingHouseRuleServiceMediaTests : IDisposable
{
    private readonly TestDatabaseFixture fixture = new();

    [Fact]
    public async Task UpsertRuleAsync_FormGenerated_ShouldLinkNewPdfAndRetirePreviousAsset()
    {
        var landlord = TestDataBuilder.BuildUser(
            email: "house-rule-media@test.local",
            displayName: "House Rule Landlord");
        var house = TestDataBuilder.BuildRoomingHouse(landlord.Id);
        var oldAsset = BuildRuleAsset(landlord.Id, MediaStatus.Linked);
        oldAsset.LinkedEntityType = nameof(RoomingHouseRule);
        oldAsset.LinkedEntityId = house.Id;
        var newAsset = BuildRuleAsset(landlord.Id, MediaStatus.Uploaded);
        var rule = new RoomingHouseRule
        {
            Id = Guid.NewGuid(),
            RoomingHouseId = house.Id,
            RoomingHouse = house,
            SourceType = RoomingHouseRuleSourceType.FormGenerated,
            MediaAssetId = oldAsset.Id,
            MediaAsset = oldAsset,
            GeneralRules = "Nội quy cũ",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        house.HouseRule = rule;
        fixture.Context.Users.Add(landlord);
        fixture.Context.RoomingHouses.Add(house);
        fixture.Context.MediaAssets.AddRange(oldAsset, newAsset);
        fixture.Context.RoomingHouseRules.Add(rule);
        await fixture.Context.SaveChangesAsync();

        var service = new RoomingHouseRuleService(
            fixture.Context,
            new FakeFileStorageService(newAsset.Id));

        var response = await service.UpsertRuleAsync(
            house.Id,
            landlord.Id,
            new UpsertRoomingHouseRuleRequest
            {
                SourceType = RoomingHouseRuleSourceType.FormGenerated.ToString(),
                GeneralRules = "Nội quy mới"
            });

        fixture.Context.ChangeTracker.Clear();
        var savedRule = await fixture.Context.RoomingHouseRules.SingleAsync(x => x.Id == rule.Id);
        var savedOldAsset = await fixture.Context.MediaAssets.SingleAsync(x => x.Id == oldAsset.Id);
        var savedNewAsset = await fixture.Context.MediaAssets.SingleAsync(x => x.Id == newAsset.Id);

        Assert.Equal(newAsset.Id, response.MediaAssetId);
        Assert.Equal(PublicMediaPathBuilder.Build(newAsset.Id), response.PdfUrl);
        Assert.Equal(newAsset.Id, savedRule.MediaAssetId);
        Assert.Equal(MediaStatus.Linked, savedNewAsset.Status);
        Assert.Equal(MediaVisibility.Public, savedNewAsset.Visibility);
        Assert.Equal(nameof(RoomingHouseRule), savedNewAsset.LinkedEntityType);
        Assert.Equal(house.Id, savedNewAsset.LinkedEntityId);
        Assert.Equal(MediaStatus.Deleted, savedOldAsset.Status);
        Assert.Null(savedOldAsset.LinkedEntityType);
        Assert.Null(savedOldAsset.LinkedEntityId);
        Assert.NotNull(savedOldAsset.DeletedAt);
    }

    private static MediaAsset BuildRuleAsset(Guid ownerUserId, MediaStatus status)
    {
        var id = Guid.NewGuid();
        return new MediaAsset
        {
            Id = id,
            OwnerUserId = ownerUserId,
            BucketName = "local-media",
            ObjectKey = $"public/rooming-house-rule-pdfs/{id:N}.pdf",
            OriginalFileName = "house-rule.pdf",
            StoredFileName = "house-rule.pdf",
            ContentType = "application/pdf",
            FileSize = 256,
            Scope = MediaScope.RoomingHouseRulePdf,
            Visibility = MediaVisibility.Public,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Dispose()
    {
        fixture.Dispose();
    }

    private sealed class FakeFileStorageService(Guid mediaAssetId) : IFileStorageService
    {
        public Task<FileUploadResponse> UploadPdfAsync(
            Stream content,
            string fileName,
            FileUploadScope scope,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(FileUploadScope.HouseRule, scope);
            return Task.FromResult(new FileUploadResponse
            {
                MediaAssetId = mediaAssetId,
                Url = PublicMediaPathBuilder.Build(mediaAssetId)
            });
        }

        public Task<FileUploadResponse> UploadImageAsync(
            ImageUploadFile file,
            FileUploadScope scope,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FileUploadResponse> UploadPdfAsync(
            ImageUploadFile file,
            FileUploadScope scope,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FileUploadResponse> UploadFileAsync(
            Stream content,
            string fileName,
            string? contentType,
            FileUploadScope scope,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
