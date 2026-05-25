using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.LegalDocuments;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomingHouses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.Users;
using SmartRentalPlatform.Domain.Enums;
using System.Security.Cryptography;
using System.Text;

namespace SmartRentalPlatform.Application.RoomingHouses
{
    public class RoomingHouseService : IRoomingHouseService
    {
        private readonly IAppDbContext context;

        public RoomingHouseService(IAppDbContext context)
        {
            this.context = context;
        }

        public async Task<RoomingHouseDetailResponse> CreateDraftAsync(
            Guid landlordUserId,
            CreateRoomingHouseDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            var activeOnboardingHouse = await BuildRoomingHouseQuery()
                .FirstOrDefaultAsync(
                    x => x.LandlordUserId == landlordUserId &&
                         (x.ApprovalStatus == RoomingHouseApprovalStatus.Draft ||
                          x.ApprovalStatus == RoomingHouseApprovalStatus.Pending ||
                          x.ApprovalStatus == RoomingHouseApprovalStatus.Rejected) &&
                         x.DeletedAt == null,
                    cancellationToken);

            if (activeOnboardingHouse?.ApprovalStatus == RoomingHouseApprovalStatus.Draft)
            {
                return ToDetailResponse(activeOnboardingHouse);
            }

            if (activeOnboardingHouse is not null)
            {
                throw new ConflictException(
                    ErrorCodes.HouseInvalidStatus,
                    "You already have a rooming house application in progress.",
                    new
                    {
                        roomingHouseId = activeOnboardingHouse.Id,
                        currentStatus = activeOnboardingHouse.ApprovalStatus.ToString()
                    });
            }

            ValidateRoomingHouseFields(request);
            var addressDisplay = await BuildAddressDisplayAsync(
                request.AddressLine,
                request.ProvinceCode,
                request.WardCode,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var roomingHouse = new RoomingHouse
            {
                Id = Guid.NewGuid(),
                LandlordUserId = landlordUserId,
                Name = request.Name.Trim(),
                Description = request.Description,
                AddressLine = request.AddressLine.Trim(),
                ProvinceCode = request.ProvinceCode.Trim(),
                WardCode = request.WardCode.Trim(),
                AddressDisplay = addressDisplay,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                ApprovalStatus = RoomingHouseApprovalStatus.Draft,
                VisibilityStatus = RoomingHouseVisibilityStatus.Hidden,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.RoomingHouses.Add(roomingHouse);
            await context.SaveChangesAsync(cancellationToken);

            var draft = await GetByIdAsync(roomingHouse.Id, cancellationToken)
                ?? throw new InternalServerException(
                    ErrorCodes.InternalServerError,
                    "Rooming house draft was created but cannot be loaded.",
                    new { roomingHouseId = roomingHouse.Id });

            return draft;
        }

        public async Task<RoomingHouseOnboardingResponse> GetOnboardingAsync(
            Guid landlordUserId,
            CancellationToken cancellationToken = default)
        {
            var houses = await BuildRoomingHouseQuery()
                .Where(x => x.LandlordUserId == landlordUserId && x.DeletedAt == null)
                .ToListAsync(cancellationToken);

            var house = houses
                .OrderBy(x => GetOnboardingPriority(x.ApprovalStatus))
                .ThenByDescending(x => x.UpdatedAt)
                .FirstOrDefault();

            if (house is null)
            {
                return new RoomingHouseOnboardingResponse
                {
                    Status = RoomingHouseOnboardingStatus.None,
                    HasRoomingHouse = false,
                    CanCreateDraft = true,
                    CanEdit = false,
                    CanSubmit = false,
                    CanEnterLandlordDashboard = false
                };
            }

            var detail = ToDetailResponse(house);
            var status = house.ApprovalStatus.ToString();

            return new RoomingHouseOnboardingResponse
            {
                Status = status,
                HasRoomingHouse = true,
                CanCreateDraft = CanCreateDraft(houses),
                CanEdit = CanEditRejectedOrDraft(house),
                CanSubmit = CanSubmit(house),
                CanEnterLandlordDashboard = houses.Any(x => x.ApprovalStatus == RoomingHouseApprovalStatus.Approved),
                RoomingHouseId = house.Id,
                RoomingHouse = detail
            };
        }

        public async Task<List<RoomingHouseResponse>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var houses = await BuildRoomingHouseQuery()
                .Where(x => x.DeletedAt == null)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            return houses.Select(ToResponse).ToList();
        }

        public async Task<RoomingHouseDetailResponse?> GetByIdAsync(
            Guid roomingHouseId,
            CancellationToken cancellationToken = default)
        {
            var house = await BuildRoomingHouseQuery()
                .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

            return house is null ? null : ToDetailResponse(house);
        }

        public async Task<List<RoomingHouseResponse>> GetByLandlordAsync(
            Guid landlordUserId,
            CancellationToken cancellationToken = default)
        {
            var houses = await BuildRoomingHouseQuery()
                .Where(x => x.LandlordUserId == landlordUserId && x.DeletedAt == null)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            return houses.Select(ToResponse).ToList();
        }

        public async Task<RoomingHouseDetailResponse?> UpdateAsync(
            Guid roomingHouseId,
            UpdateRoomingHouseRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateRoomingHouseFields(request);
            var addressDisplay = await BuildAddressDisplayAsync(
                request.AddressLine,
                request.ProvinceCode,
                request.WardCode,
                cancellationToken);

            var roomingHouse = await context.RoomingHouses
                .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

            if (roomingHouse is null)
            {
                return null;
            }

            EnsureEditable(roomingHouse);

            roomingHouse.Name = request.Name.Trim();
            roomingHouse.Description = request.Description;
            roomingHouse.AddressLine = request.AddressLine.Trim();
            roomingHouse.ProvinceCode = request.ProvinceCode.Trim();
            roomingHouse.WardCode = request.WardCode.Trim();
            roomingHouse.AddressDisplay = addressDisplay;
            roomingHouse.Latitude = request.Latitude;
            roomingHouse.Longitude = request.Longitude;
            roomingHouse.UpdatedAt = DateTimeOffset.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            return await GetByIdAsync(roomingHouseId, cancellationToken);
        }

        public async Task<RoomingHouseDetailResponse?> UpdateAmenitiesAsync(
            Guid roomingHouseId,
            UpdateAmenitiesRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await context.BeginTransactionAsync(cancellationToken);

            try
            {
                var roomingHouse = await context.RoomingHouses
                    .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

                if (roomingHouse is null)
                {
                    return null;
                }

                EnsureEditable(roomingHouse);

                var amenityIds = await ValidateHouseAmenityIdsAsync(request.AmenityIds, cancellationToken);
                var currentAmenities = await context.RoomingHouseAmenities
                    .Where(x => x.RoomingHouseId == roomingHouseId)
                    .ToListAsync(cancellationToken);

                context.RoomingHouseAmenities.RemoveRange(currentAmenities);
                AddAmenities(roomingHouseId, amenityIds);

                roomingHouse.UpdatedAt = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return await GetByIdAsync(roomingHouseId, cancellationToken);
        }

        public async Task<RoomingHouseDetailResponse?> UpdateImagesAsync(
            Guid roomingHouseId,
            UpdatePropertyImagesRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await context.BeginTransactionAsync(cancellationToken);

            try
            {
                var roomingHouse = await context.RoomingHouses
                    .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

                if (roomingHouse is null)
                {
                    return null;
                }

                EnsureEditable(roomingHouse);
                ValidatePropertyImages(request.Images);

                var requestImageIds = request.Images
                    .Where(x => x.Id.HasValue)
                    .Select(x => x.Id!.Value)
                    .ToHashSet();

                if (requestImageIds.Count != request.Images.Count(x => x.Id.HasValue))
                {
                    throw new BadRequestException(
                        ErrorCodes.ValidationError,
                        "Duplicate image ids are not allowed.",
                        new { field = nameof(request.Images) });
                }

                var currentImages = await context.PropertyImages
                    .Where(x => x.RoomingHouseId == roomingHouseId)
                    .ToListAsync(cancellationToken);

                var currentImageIds = currentImages.Select(x => x.Id).ToHashSet();
                var invalidImageIds = requestImageIds.Where(id => !currentImageIds.Contains(id)).ToList();

                if (invalidImageIds.Count > 0)
                {
                    throw new BadRequestException(
                        ErrorCodes.ImageInvalidOwner,
                        "One or more image ids are invalid.",
                        new { imageIds = invalidImageIds });
                }

                var imagesToDelete = currentImages
                    .Where(x => !requestImageIds.Contains(x.Id))
                    .ToList();

                context.PropertyImages.RemoveRange(imagesToDelete);

                foreach (var imageRequest in request.Images)
                {
                    var objectKey = imageRequest.ObjectKey.Trim();

                    if (imageRequest.Id.HasValue)
                    {
                        var existingImage = currentImages.First(x => x.Id == imageRequest.Id.Value);
                        existingImage.ObjectKey = objectKey;
                        existingImage.ImageUrl = BuildImageUrl(objectKey);
                        existingImage.Caption = imageRequest.Caption;
                        existingImage.IsCover = imageRequest.IsCover;
                        existingImage.SortOrder = imageRequest.SortOrder;
                    }
                    else
                    {
                        context.PropertyImages.Add(new PropertyImage
                        {
                            Id = Guid.NewGuid(),
                            RoomingHouseId = roomingHouseId,
                            RoomId = null,
                            ObjectKey = objectKey,
                            ImageUrl = BuildImageUrl(objectKey),
                            Caption = imageRequest.Caption,
                            IsCover = imageRequest.IsCover,
                            SortOrder = imageRequest.SortOrder,
                            CreatedAt = DateTimeOffset.UtcNow
                        });
                    }
                }

                roomingHouse.UpdatedAt = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return await GetByIdAsync(roomingHouseId, cancellationToken);
        }

        public async Task<RoomingHouseDetailResponse?> UpdateLegalDocumentAsync(
            Guid roomingHouseId,
            UpdateRoomingHouseLegalDocumentRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await context.BeginTransactionAsync(cancellationToken);

            try
            {
                var roomingHouse = await context.RoomingHouses
                    .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

                if (roomingHouse is null)
                {
                    return null;
                }

                EnsureLegalDocumentEditable(roomingHouse);

                var documentType = ValidateLegalDocument(request);
                var now = DateTimeOffset.UtcNow;
                var documentNumber = request.DocumentNumber.Trim();

                var legalDocument = await context.RoomingHouseLegalDocuments
                    .FirstOrDefaultAsync(x => x.RoomingHouseId == roomingHouseId, cancellationToken);

                if (legalDocument is null)
                {
                    legalDocument = new RoomingHouseLegalDocument
                    {
                        RoomingHouseId = roomingHouseId,
                        CreatedAt = now
                    };

                    context.RoomingHouseLegalDocuments.Add(legalDocument);
                }

                legalDocument.DocumentType = documentType;
                legalDocument.FrontImageObjectKey = request.FrontImageObjectKey.Trim();
                legalDocument.BackImageObjectKey = request.BackImageObjectKey.Trim();
                legalDocument.ExtraImageObjectKey = NormalizeOptionalObjectKey(request.ExtraImageObjectKey);
                legalDocument.DocumentNumberMasked = MaskDocumentNumber(documentNumber);
                legalDocument.DocumentNumberHash = HashDocumentNumber(documentNumber);
                legalDocument.UploadedAt = now;
                legalDocument.UpdatedAt = now;

                roomingHouse.UpdatedAt = now;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return await GetByIdAsync(roomingHouseId, cancellationToken);
        }

        public async Task<RoomingHouseDetailResponse?> SubmitAsync(
            Guid roomingHouseId,
            Guid landlordUserId,
            CancellationToken cancellationToken = default)
        {
            var roomingHouse = await context.RoomingHouses
                .Include(x => x.LegalDocument)
                .Include(x => x.Images)
                .Include(x => x.RoomingHouseAmenities)
                    .ThenInclude(x => x.Amenity)
                .FirstOrDefaultAsync(
                    x => x.Id == roomingHouseId &&
                         x.LandlordUserId == landlordUserId &&
                         x.DeletedAt == null,
                    cancellationToken);

            if (roomingHouse is null)
            {
                return null;
            }

            if (roomingHouse.ApprovalStatus is not RoomingHouseApprovalStatus.Draft and not RoomingHouseApprovalStatus.Rejected)
            {
                throw new ConflictException(
                    ErrorCodes.HouseInvalidStatus,
                    "Only draft or rejected rooming houses can be submitted.",
                    new { currentStatus = roomingHouse.ApprovalStatus.ToString() });
            }

            await ValidateFullRoomingHouseAsync(roomingHouse, cancellationToken);

            roomingHouse.ApprovalStatus = RoomingHouseApprovalStatus.Pending;
            roomingHouse.VisibilityStatus = RoomingHouseVisibilityStatus.Hidden;
            roomingHouse.RejectedReason = null;
            roomingHouse.ReviewedByAdminId = null;
            roomingHouse.ReviewedAt = null;
            roomingHouse.UpdatedAt = DateTimeOffset.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            return ToDetailResponse(roomingHouse);
        }

        public async Task<RoomingHouseDetailResponse?> ApproveAsync(
            Guid roomingHouseId,
            Guid adminUserId,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await context.BeginTransactionAsync(cancellationToken);

            try
            {
                var roomingHouse = await context.RoomingHouses
                    .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

                if (roomingHouse is null)
                {
                    return null;
                }

                if (roomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Pending)
                {
                    throw new ConflictException(
                        ErrorCodes.HouseInvalidStatus,
                        "Only pending rooming houses can be approved.",
                        new { currentStatus = roomingHouse.ApprovalStatus.ToString() });
                }

                var now = DateTimeOffset.UtcNow;
                roomingHouse.ApprovalStatus = RoomingHouseApprovalStatus.Approved;
                roomingHouse.VisibilityStatus = RoomingHouseVisibilityStatus.Visible;
                roomingHouse.RejectedReason = null;
                roomingHouse.ReviewedByAdminId = adminUserId;
                roomingHouse.ReviewedAt = now;
                roomingHouse.UpdatedAt = now;

                await GrantLandlordRoleIfMissingAsync(roomingHouse.LandlordUserId, now, cancellationToken);

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return await GetByIdAsync(roomingHouseId, cancellationToken);
        }

        public async Task<RoomingHouseDetailResponse?> RejectAsync(
            Guid roomingHouseId,
            Guid adminUserId,
            RejectRoomingHouseRequest request,
            CancellationToken cancellationToken = default)
        {
            var roomingHouse = await context.RoomingHouses
                .FirstOrDefaultAsync(x => x.Id == roomingHouseId && x.DeletedAt == null, cancellationToken);

            if (roomingHouse is null)
            {
                return null;
            }

            if (roomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Pending)
            {
                throw new ConflictException(
                    ErrorCodes.HouseInvalidStatus,
                    "Only pending rooming houses can be rejected.",
                    new { currentStatus = roomingHouse.ApprovalStatus.ToString() });
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new BadRequestException(
                    ErrorCodes.HouseRejectReasonRequired,
                    "Reject reason is required.",
                    new { field = nameof(request.Reason) });
            }

            var now = DateTimeOffset.UtcNow;
            roomingHouse.ApprovalStatus = RoomingHouseApprovalStatus.Rejected;
            roomingHouse.VisibilityStatus = RoomingHouseVisibilityStatus.Hidden;
            roomingHouse.RejectedReason = request.Reason.Trim();
            roomingHouse.ReviewedByAdminId = adminUserId;
            roomingHouse.ReviewedAt = now;
            roomingHouse.UpdatedAt = now;

            await context.SaveChangesAsync(cancellationToken);

            return await GetByIdAsync(roomingHouseId, cancellationToken);
        }

        private IQueryable<RoomingHouse> BuildRoomingHouseQuery()
        {
            return context.RoomingHouses
                .AsNoTracking()
                .Include(x => x.Province)
                .Include(x => x.Ward)
                .Include(x => x.LegalDocument)
                .Include(x => x.Images)
                .Include(x => x.RoomingHouseAmenities)
                    .ThenInclude(x => x.Amenity);
        }

        private async Task ValidateFullRoomingHouseAsync(
            RoomingHouse roomingHouse,
            CancellationToken cancellationToken)
        {
            ValidateRoomingHouseFields(
                roomingHouse.Name,
                roomingHouse.AddressLine,
                roomingHouse.ProvinceCode,
                roomingHouse.WardCode,
                roomingHouse.Latitude,
                roomingHouse.Longitude);

            await ValidateAddressAsync(
                roomingHouse.ProvinceCode,
                roomingHouse.WardCode,
                cancellationToken);

            ValidateRequiredPropertyImages(roomingHouse.Images, "Rooming house images");

            if (roomingHouse.LegalDocument is null)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Legal document is required before submitting.",
                    new { field = nameof(roomingHouse.LegalDocument) });
            }

            ValidateLegalDocumentFields(
                roomingHouse.LegalDocument.DocumentType.ToString(),
                roomingHouse.LegalDocument.FrontImageObjectKey,
                roomingHouse.LegalDocument.BackImageObjectKey,
                roomingHouse.LegalDocument.DocumentNumberMasked);
        }

        private static void ValidateRequiredPropertyImages(
            IEnumerable<PropertyImage> images,
            string fieldName)
        {
            var imageList = images.ToList();

            if (imageList.Count < 3)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "At least 3 images are required before submitting.",
                    new { field = fieldName });
            }

            var coverCount = imageList.Count(x => x.IsCover);

            if (coverCount != 1)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Exactly one cover image is required before submitting.",
                    new { field = fieldName });
            }

            if (imageList.Any(x => string.IsNullOrWhiteSpace(x.ObjectKey)))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Image object key is required.",
                    new { field = fieldName });
            }
        }

        private async Task GrantLandlordRoleIfMissingAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var landlordRole = await context.Roles
                .FirstAsync(x => x.Name == RoleName.Landlord, cancellationToken);

            var hasLandlordRole = await context.UserRoles
                .AnyAsync(x => x.UserId == userId && x.RoleId == landlordRole.Id, cancellationToken);

            if (hasLandlordRole)
            {
                return;
            }

            context.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = landlordRole.Id,
                CreatedAt = now
            });
        }

        private static int GetOnboardingPriority(RoomingHouseApprovalStatus status)
        {
            return status switch
            {
                RoomingHouseApprovalStatus.Draft => 0,
                RoomingHouseApprovalStatus.Rejected => 1,
                RoomingHouseApprovalStatus.Pending => 2,
                RoomingHouseApprovalStatus.Approved => 3,
                _ => 4
            };
        }

        private static bool CanEditRejectedOrDraft(RoomingHouse house)
        {
            return house.ApprovalStatus is RoomingHouseApprovalStatus.Draft or RoomingHouseApprovalStatus.Rejected;
        }

        private static bool CanSubmit(RoomingHouse house)
        {
            return house.ApprovalStatus is RoomingHouseApprovalStatus.Draft or RoomingHouseApprovalStatus.Rejected;
        }

        private static bool CanCreateDraft(IEnumerable<RoomingHouse> houses)
        {
            return !houses.Any(x =>
                x.ApprovalStatus is RoomingHouseApprovalStatus.Draft
                    or RoomingHouseApprovalStatus.Pending
                    or RoomingHouseApprovalStatus.Rejected);
        }

        private static void EnsureEditable(RoomingHouse roomingHouse)
        {
            if (roomingHouse.ApprovalStatus == RoomingHouseApprovalStatus.Pending)
            {
                throw new ConflictException(
                    ErrorCodes.HouseInvalidStatus,
                    "Pending rooming houses cannot be updated.",
                    new { currentStatus = roomingHouse.ApprovalStatus.ToString() });
            }
        }

        private static void EnsureLegalDocumentEditable(RoomingHouse roomingHouse)
        {
            if (roomingHouse.ApprovalStatus is not RoomingHouseApprovalStatus.Draft and not RoomingHouseApprovalStatus.Rejected)
            {
                throw new ConflictException(
                    ErrorCodes.HouseInvalidStatus,
                    "Legal document can only be updated while draft or rejected.",
                    new { currentStatus = roomingHouse.ApprovalStatus.ToString() });
            }
        }

        private async Task ValidateAddressAsync(
            string provinceCode,
            string wardCode,
            CancellationToken cancellationToken)
        {
            var province = provinceCode.Trim();
            var ward = wardCode.Trim();

            var provinceExists = await context.AdministrativeProvinces
                .AnyAsync(x => x.Code == province && x.IsActive, cancellationToken);

            if (!provinceExists)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Province is invalid.",
                    new { field = nameof(provinceCode) });
            }

            var wardExists = await context.AdministrativeWards
                .AnyAsync(x => x.Code == ward && x.ProvinceCode == province && x.IsActive, cancellationToken);

            if (!wardExists)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Ward is invalid.",
                    new { field = nameof(wardCode) });
            }
        }

        private async Task<string> BuildAddressDisplayAsync(
            string addressLine,
            string provinceCode,
            string wardCode,
            CancellationToken cancellationToken)
        {
            var province = provinceCode.Trim();
            var ward = wardCode.Trim();

            var provinceName = await context.AdministrativeProvinces
                .Where(x => x.Code == province && x.IsActive)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);

            if (provinceName is null)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Province is invalid.",
                    new { field = nameof(provinceCode) });
            }

            var wardName = await context.AdministrativeWards
                .Where(x => x.Code == ward && x.ProvinceCode == province && x.IsActive)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);

            if (wardName is null)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Ward is invalid.",
                    new { field = nameof(wardCode) });
            }

            return $"{addressLine.Trim()}, {wardName}, {provinceName}";
        }

        private static string BuildAddressDisplay(RoomingHouse house)
        {
            if (!string.IsNullOrWhiteSpace(house.Ward?.Name) &&
                !string.IsNullOrWhiteSpace(house.Province?.Name))
            {
                return $"{house.AddressLine}, {house.Ward.Name}, {house.Province.Name}";
            }

            return house.AddressDisplay;
        }

        private async Task<List<int>> ValidateHouseAmenityIdsAsync(
            IEnumerable<int> requestedAmenityIds,
            CancellationToken cancellationToken)
        {
            var amenityIds = requestedAmenityIds.Distinct().ToList();
            var validAmenityIds = await context.Amenities
                .Where(x =>
                    amenityIds.Contains(x.Id) &&
                    x.IsActive &&
                    (x.Scope == AmenityScope.House || x.Scope == AmenityScope.Both))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (validAmenityIds.Count != amenityIds.Count)
            {
                throw new BadRequestException(
                    ErrorCodes.AmenityNotFound,
                    "One or more amenity ids are invalid.",
                    new { amenityIds });
            }

            return amenityIds;
        }

        private static void ValidateRoomingHouseFields(RoomingHouseBasicInfoRequest request)
        {
            ValidateRoomingHouseFields(
                request.Name,
                request.AddressLine,
                request.ProvinceCode,
                request.WardCode,
                request.Latitude,
                request.Longitude);
        }

        private static void ValidateRoomingHouseFields(
            string name,
            string addressLine,
            string provinceCode,
            string wardCode,
            decimal? latitude,
            decimal? longitude)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Rooming house name is required.",
                    new { field = nameof(name) });
            }

            if (string.IsNullOrWhiteSpace(addressLine))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Address line is required.",
                    new { field = nameof(addressLine) });
            }

            if (string.IsNullOrWhiteSpace(provinceCode))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Province code is required.",
                    new { field = nameof(provinceCode) });
            }

            if (string.IsNullOrWhiteSpace(wardCode))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Ward code is required.",
                    new { field = nameof(wardCode) });
            }

            if (latitude is < -90 or > 90)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Latitude must be between -90 and 90.",
                    new { field = nameof(latitude) });
            }

            if (longitude is < -180 or > 180)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Longitude must be between -180 and 180.",
                    new { field = nameof(longitude) });
            }
        }

        private static LegalDocumentType ValidateLegalDocument(
            UpdateRoomingHouseLegalDocumentRequest legalDocument)
        {
            return ValidateLegalDocumentFields(
                legalDocument.DocumentType,
                legalDocument.FrontImageObjectKey,
                legalDocument.BackImageObjectKey,
                legalDocument.DocumentNumber);
        }

        private static LegalDocumentType ValidateLegalDocumentFields(
            string documentTypeValue,
            string frontImageObjectKey,
            string backImageObjectKey,
            string documentNumber)
        {
            if (!Enum.TryParse<LegalDocumentType>(documentTypeValue, ignoreCase: true, out var documentType))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Invalid legal document type.",
                    new { field = nameof(documentTypeValue) });
            }

            if (string.IsNullOrWhiteSpace(frontImageObjectKey))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Front image object key is required.",
                    new { field = nameof(frontImageObjectKey) });
            }

            if (string.IsNullOrWhiteSpace(backImageObjectKey))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Back image object key is required.",
                    new { field = nameof(backImageObjectKey) });
            }

            if (string.IsNullOrWhiteSpace(documentNumber))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Document number is required.",
                    new { field = nameof(documentNumber) });
            }

            return documentType;
        }

        private static void ValidatePropertyImages(IReadOnlyCollection<UpdatePropertyImageItemRequest> images)
        {
            if (images.Count < 3)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "At least 3 images are required.",
                    new { field = nameof(images) });
            }

            ValidateCoverImageCount(images.Count(x => x.IsCover));

            if (images.Any(x => string.IsNullOrWhiteSpace(x.ObjectKey)))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Image object key is required.",
                    new { field = nameof(images) });
            }
        }

        private static void ValidateCoverImageCount(int coverCount)
        {
            if (coverCount != 1)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Exactly one cover image is required.",
                    new { field = "Images" });
            }
        }

        private void AddAmenities(Guid roomingHouseId, IEnumerable<int> amenityIds)
        {
            foreach (var amenityId in amenityIds)
            {
                context.RoomingHouseAmenities.Add(new RoomingHouseAmenity
                {
                    AmenityId = amenityId,
                    RoomingHouseId = roomingHouseId
                });
            }
        }

        private static RoomingHouseResponse ToResponse(RoomingHouse house)
        {
            return new RoomingHouseResponse
            {
                Id = house.Id,
                LandlordUserId = house.LandlordUserId,
                Name = house.Name,
                AddressDisplay = BuildAddressDisplay(house),
                ApprovalStatus = house.ApprovalStatus.ToString(),
                VisibilityStatus = house.VisibilityStatus.ToString(),
                RejectedReason = house.RejectedReason,
                CreatedAt = house.CreatedAt,
                UpdatedAt = house.UpdatedAt
            };
        }

        private static RoomingHouseDetailResponse ToDetailResponse(RoomingHouse house)
        {
            return new RoomingHouseDetailResponse
            {
                Id = house.Id,
                LandlordUserId = house.LandlordUserId,
                Name = house.Name,
                Description = house.Description,
                AddressLine = house.AddressLine,
                ProvinceCode = house.ProvinceCode,
                WardCode = house.WardCode,
                AddressDisplay = BuildAddressDisplay(house),
                Latitude = house.Latitude,
                Longitude = house.Longitude,
                ApprovalStatus = house.ApprovalStatus.ToString(),
                VisibilityStatus = house.VisibilityStatus.ToString(),
                RejectedReason = house.RejectedReason,
                ReviewedByAdminId = house.ReviewedByAdminId,
                ReviewedAt = house.ReviewedAt,
                CreatedAt = house.CreatedAt,
                UpdatedAt = house.UpdatedAt,
                LegalDocument = house.LegalDocument is null ? null : new RoomingHouseLegalDocumentResponse
                {
                    RoomingHouseId = house.LegalDocument.RoomingHouseId,
                    DocumentType = house.LegalDocument.DocumentType.ToString(),
                    FrontImageObjectKey = house.LegalDocument.FrontImageObjectKey,
                    BackImageObjectKey = house.LegalDocument.BackImageObjectKey,
                    ExtraImageObjectKey = house.LegalDocument.ExtraImageObjectKey,
                    DocumentNumberMasked = house.LegalDocument.DocumentNumberMasked,
                    UploadedAt = house.LegalDocument.UploadedAt,
                    CreatedAt = house.LegalDocument.CreatedAt,
                    UpdatedAt = house.LegalDocument.UpdatedAt
                },
                Images = house.Images
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new PropertyImageResponse
                    {
                        Id = x.Id,
                        ObjectKey = x.ObjectKey,
                        ImageUrl = x.ImageUrl,
                        Caption = x.Caption,
                        IsCover = x.IsCover,
                        SortOrder = x.SortOrder,
                        CreatedAt = x.CreatedAt
                    })
                    .ToList(),
                Amenities = house.RoomingHouseAmenities
                    .Select(x => new AmenityResponse
                    {
                        Id = x.Amenity.Id,
                        Name = x.Amenity.Name,
                        Scope = x.Amenity.Scope.ToString(),
                        IconCode = x.Amenity.IconCode
                    })
                    .ToList()
            };
        }

        private static string MaskDocumentNumber(string documentNumber)
        {
            if (documentNumber.Length <= 4)
            {
                return new string('*', documentNumber.Length);
            }

            return new string('*', documentNumber.Length - 4) + documentNumber[^4..];
        }

        private static string HashDocumentNumber(string documentNumber)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(documentNumber));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string BuildImageUrl(string objectKey)
        {
            return $"/uploads/{objectKey}";
        }

        private static string? NormalizeOptionalObjectKey(string? objectKey)
        {
            return string.IsNullOrWhiteSpace(objectKey) ? null : objectKey.Trim();
        }
    }
}
