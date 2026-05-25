using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Amenities;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.PropertyImages;
using SmartRentalPlatform.Contracts.RoomPriceTiers;
using SmartRentalPlatform.Contracts.Rooms;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;

namespace SmartRentalPlatform.Application.Rooms
{
    public class RoomService : IRoomService
    {
        private readonly IAppDbContext context;

        public RoomService(IAppDbContext context)
        {
            this.context = context;
        }

        public async Task<RoomResponse> CreateAsync(
            Guid landlordUserId,
            Guid roomingHouseId,
            CreateRoomRequest request,
            CancellationToken cancellationToken = default)
        {
            await EnsureOwnedApprovedRoomingHouseAsync(landlordUserId, roomingHouseId, cancellationToken);

            ValidateRoomFields(request.RoomNumber, request.Floor, request.AreaM2, request.MaxOccupants);

            var roomNumber = request.RoomNumber.Trim();
            await EnsureRoomNumberAvailableAsync(roomingHouseId, roomNumber, excludedRoomId: null, cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var room = new Room
            {
                Id = Guid.NewGuid(),
                RoomingHouseId = roomingHouseId,
                RoomNumber = roomNumber,
                Floor = request.Floor,
                AreaM2 = request.AreaM2,
                MaxOccupants = request.MaxOccupants,
                Description = request.Description,
                Status = RoomStatus.Hidden,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.Rooms.Add(room);
            await context.SaveChangesAsync(cancellationToken);

            return await GetByIdAsync(landlordUserId, room.Id, cancellationToken)
                ?? throw new InternalServerException(
                    ErrorCodes.InternalServerError,
                    "Đã tạo phòng nhưng không thể tải lại thông tin phòng.",
                    new { roomId = room.Id });
        }

        public async Task<List<RoomResponse>> GetByRoomingHouseAsync(
            Guid landlordUserId,
            Guid roomingHouseId,
            CancellationToken cancellationToken = default)
        {
            await EnsureOwnedApprovedRoomingHouseAsync(landlordUserId, roomingHouseId, cancellationToken);

            var rooms = await BuildRoomQuery()
                .Where(x => x.RoomingHouseId == roomingHouseId && x.DeletedAt == null)
                .OrderBy(x => x.Floor)
                .ThenBy(x => x.RoomNumber)
                .ToListAsync(cancellationToken);

            return rooms.Select(ToResponse).ToList();
        }

        public async Task<RoomResponse?> GetByIdAsync(
            Guid landlordUserId,
            Guid roomId,
            CancellationToken cancellationToken = default)
        {
            var room = await BuildRoomQuery()
                .FirstOrDefaultAsync(
                    x => x.Id == roomId &&
                         x.DeletedAt == null &&
                         x.RoomingHouse.LandlordUserId == landlordUserId,
                    cancellationToken);

            return room is null ? null : ToResponse(room);
        }

        public async Task<RoomResponse?> UpdateAsync(
            Guid landlordUserId,
            Guid roomId,
            UpdateRoomRequest request,
            CancellationToken cancellationToken = default)
        {
            var room = await GetOwnedRoomForUpdateAsync(landlordUserId, roomId, cancellationToken);
            if (room is null)
            {
                return null;
            }

            EnsureRoomingHouseApproved(room.RoomingHouse);
            ValidateRoomFields(request.RoomNumber, request.Floor, request.AreaM2, request.MaxOccupants);
            await EnsurePriceTiersFitMaxOccupantsAsync(roomId, request.MaxOccupants, cancellationToken);

            var roomNumber = request.RoomNumber.Trim();
            await EnsureRoomNumberAvailableAsync(room.RoomingHouseId, roomNumber, roomId, cancellationToken);

            room.RoomNumber = roomNumber;
            room.Floor = request.Floor;
            room.AreaM2 = request.AreaM2;
            room.MaxOccupants = request.MaxOccupants;
            room.Description = request.Description;
            room.UpdatedAt = DateTimeOffset.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            return await GetByIdAsync(landlordUserId, roomId, cancellationToken);
        }

        public async Task<RoomResponse?> UpdateAmenitiesAsync(
            Guid landlordUserId,
            Guid roomId,
            UpdateAmenitiesRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await context.BeginTransactionAsync(cancellationToken);

            try
            {
                var room = await GetOwnedRoomForUpdateAsync(landlordUserId, roomId, cancellationToken);
                if (room is null)
                {
                    return null;
                }

                EnsureRoomingHouseApproved(room.RoomingHouse);

                var amenityIds = await ValidateRoomAmenityIdsAsync(request.AmenityIds, cancellationToken);
                var currentAmenities = await context.RoomAmenities
                    .Where(x => x.RoomId == roomId)
                    .ToListAsync(cancellationToken);

                context.RoomAmenities.RemoveRange(currentAmenities);

                foreach (var amenityId in amenityIds)
                {
                    context.RoomAmenities.Add(new RoomAmenity
                    {
                        RoomId = roomId,
                        AmenityId = amenityId
                    });
                }

                room.UpdatedAt = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return await GetByIdAsync(landlordUserId, roomId, cancellationToken);
        }

        public async Task<RoomResponse?> UpdateImagesAsync(
            Guid landlordUserId,
            Guid roomId,
            UpdatePropertyImagesRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await context.BeginTransactionAsync(cancellationToken);

            try
            {
                var room = await GetOwnedRoomForUpdateAsync(landlordUserId, roomId, cancellationToken);
                if (room is null)
                {
                    return null;
                }

                EnsureRoomingHouseApproved(room.RoomingHouse);
                ValidatePropertyImages(request.Images);

                var requestImageIds = request.Images
                    .Where(x => x.Id.HasValue)
                    .Select(x => x.Id!.Value)
                    .ToHashSet();

                if (requestImageIds.Count != request.Images.Count(x => x.Id.HasValue))
                {
                    throw new BadRequestException(
                        ErrorCodes.ValidationError,
                        "Không được gửi trùng mã ảnh.",
                        new { field = nameof(request.Images) });
                }

                var currentImages = await context.PropertyImages
                    .Where(x => x.RoomId == roomId)
                    .ToListAsync(cancellationToken);

                var currentImageIds = currentImages.Select(x => x.Id).ToHashSet();
                var invalidImageIds = requestImageIds.Where(id => !currentImageIds.Contains(id)).ToList();

                if (invalidImageIds.Count > 0)
                {
                    throw new BadRequestException(
                        ErrorCodes.ImageInvalidOwner,
                        "Một hoặc nhiều mã ảnh không hợp lệ.",
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
                            RoomId = roomId,
                            ObjectKey = objectKey,
                            ImageUrl = BuildImageUrl(objectKey),
                            Caption = imageRequest.Caption,
                            IsCover = imageRequest.IsCover,
                            SortOrder = imageRequest.SortOrder,
                            CreatedAt = DateTimeOffset.UtcNow
                        });
                    }
                }

                room.UpdatedAt = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return await GetByIdAsync(landlordUserId, roomId, cancellationToken);
        }

        public async Task<RoomResponse?> UpdatePriceTiersAsync(
            Guid landlordUserId,
            Guid roomId,
            UpdateRoomPriceTiersRequest request,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await context.BeginTransactionAsync(cancellationToken);

            try
            {
                var room = await GetOwnedRoomForUpdateAsync(landlordUserId, roomId, cancellationToken);
                if (room is null)
                {
                    return null;
                }

                EnsureRoomingHouseApproved(room.RoomingHouse);
                ValidatePriceTiers(request.PriceTiers, room.MaxOccupants);

                var currentPriceTiers = await context.RoomPriceTiers
                    .Where(x => x.RoomId == roomId)
                    .ToListAsync(cancellationToken);

                context.RoomPriceTiers.RemoveRange(currentPriceTiers);

                var now = DateTimeOffset.UtcNow;
                foreach (var tier in request.PriceTiers)
                {
                    context.RoomPriceTiers.Add(new RoomPriceTier
                    {
                        Id = Guid.NewGuid(),
                        RoomId = roomId,
                        OccupantCount = tier.OccupantCount,
                        MonthlyRent = tier.MonthlyRent,
                        IsActive = tier.IsActive,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }

                room.UpdatedAt = now;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return await GetByIdAsync(landlordUserId, roomId, cancellationToken);
        }

        public async Task<RoomResponse?> SubmitAsync(
            Guid landlordUserId,
            Guid roomId,
            CancellationToken cancellationToken = default)
        {
            var room = await context.Rooms
                .Include(x => x.RoomingHouse)
                .Include(x => x.PriceTiers)
                .Include(x => x.Images)
                .FirstOrDefaultAsync(
                    x => x.Id == roomId &&
                         x.DeletedAt == null &&
                         x.RoomingHouse.LandlordUserId == landlordUserId,
                    cancellationToken);

            if (room is null)
            {
                return null;
            }

            EnsureRoomingHouseApproved(room.RoomingHouse);

            if (room.Status != RoomStatus.Hidden)
            {
                throw new ConflictException(
                    ErrorCodes.RoomInvalidStatus,
                    "Chỉ phòng đang ẩn mới có thể gửi hiển thị.",
                    new { currentStatus = room.Status.ToString() });
            }
            ValidateRoomFields(
                room.RoomNumber,
                room.Floor,
                room.AreaM2,
                room.MaxOccupants);

            ValidateRequiredRoomImages(room.Images);

            ValidateRoomCanBeSubmitted(room);

            room.Status = RoomStatus.Available;
            room.UpdatedAt = DateTimeOffset.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            return await GetByIdAsync(landlordUserId, roomId, cancellationToken);
        }

        private static void ValidateRequiredRoomImages(IEnumerable<PropertyImage> images)
        {
            var imageList = images.ToList();

            if (imageList.Count < 3)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Phòng cần có ít nhất 3 ảnh trước khi gửi hiển thị.",
                    new { field = nameof(images) });
            }

            var coverCount = imageList.Count(x => x.IsCover);

            if (coverCount != 1)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Phòng cần có đúng 1 ảnh đại diện trước khi gửi hiển thị.",
                    new { field = nameof(images) });
            }

            if (imageList.Any(x => string.IsNullOrWhiteSpace(x.ObjectKey)))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Mã lưu trữ ảnh là bắt buộc.",
                    new { field = nameof(images) });
            }
        }

        public async Task<RoomResponse?> UpdateStatusAsync(
            Guid landlordUserId,
            Guid roomId,
            UpdateRoomStatusRequest request,
            CancellationToken cancellationToken = default)
        {
            var room = await GetOwnedRoomForUpdateAsync(landlordUserId, roomId, cancellationToken);
            if (room is null)
            {
                return null;
            }

            EnsureRoomingHouseApproved(room.RoomingHouse);

            if (room.Status == RoomStatus.Hidden)
            {
                throw new ConflictException(
                    ErrorCodes.RoomInvalidStatus,
                    "Phòng đang ẩn phải được gửi hiển thị trước khi thay đổi trạng thái vận hành.",
                    new { currentStatus = room.Status.ToString() });
            }

            if (!Enum.TryParse<RoomStatus>(request.Status, ignoreCase: true, out var status))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Trạng thái phòng không hợp lệ.",
                    new { field = nameof(request.Status) });
            }

            if (status == RoomStatus.Hidden)
            {
                throw new ConflictException(
                    ErrorCodes.RoomInvalidStatus,
                    "Phòng đã hiển thị không thể chuyển lại sang trạng thái ẩn bằng API trạng thái.",
                    new { requestedStatus = request.Status });
            }

            room.Status = status;
            room.UpdatedAt = DateTimeOffset.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            return await GetByIdAsync(landlordUserId, roomId, cancellationToken);
        }

        private IQueryable<Room> BuildRoomQuery()
        {
            return context.Rooms
                .AsNoTracking()
                .Include(x => x.RoomingHouse)
                .Include(x => x.PriceTiers)
                .Include(x => x.Images)
                .Include(x => x.RoomAmenities)
                    .ThenInclude(x => x.Amenity);
        }

        private async Task<Room?> GetOwnedRoomForUpdateAsync(
            Guid landlordUserId,
            Guid roomId,
            CancellationToken cancellationToken)
        {
            return await context.Rooms
                .Include(x => x.RoomingHouse)
                .FirstOrDefaultAsync(
                    x => x.Id == roomId &&
                         x.DeletedAt == null &&
                         x.RoomingHouse.LandlordUserId == landlordUserId,
                    cancellationToken);
        }

        private async Task EnsureOwnedApprovedRoomingHouseAsync(
            Guid landlordUserId,
            Guid roomingHouseId,
            CancellationToken cancellationToken)
        {
            var roomingHouse = await context.RoomingHouses
                .FirstOrDefaultAsync(
                    x => x.Id == roomingHouseId &&
                         x.LandlordUserId == landlordUserId &&
                         x.DeletedAt == null,
                    cancellationToken);

            if (roomingHouse is null)
            {
                throw new NotFoundException(
                    ErrorCodes.HouseNotFound,
                    "Không tìm thấy khu trọ.",
                    new { roomingHouseId });
            }

            EnsureRoomingHouseApproved(roomingHouse);
        }

        private static void EnsureRoomingHouseApproved(RoomingHouse roomingHouse)
        {
            if (roomingHouse.ApprovalStatus != RoomingHouseApprovalStatus.Approved)
            {
                throw new ConflictException(
                    ErrorCodes.HouseNotApproved,
                    "Chỉ khu trọ đã được duyệt mới có thể quản lý phòng.",
                    new { currentStatus = roomingHouse.ApprovalStatus.ToString() });
            }
        }

        private async Task EnsureRoomNumberAvailableAsync(
            Guid roomingHouseId,
            string roomNumber,
            Guid? excludedRoomId,
            CancellationToken cancellationToken)
        {
            var normalizedRoomNumber = roomNumber.ToLowerInvariant();
            var duplicateExists = await context.Rooms.AnyAsync(
                x => x.RoomingHouseId == roomingHouseId &&
                     x.DeletedAt == null &&
                     x.RoomNumber.ToLower() == normalizedRoomNumber &&
                     (!excludedRoomId.HasValue || x.Id != excludedRoomId.Value),
                cancellationToken);

            if (duplicateExists)
            {
                throw new ConflictException(
                    ErrorCodes.RoomDuplicateNumber,
                    "Số phòng đã tồn tại trong khu trọ này.",
                    new { roomingHouseId, roomNumber });
            }
        }

        private async Task<List<int>> ValidateRoomAmenityIdsAsync(
            IEnumerable<int> requestedAmenityIds,
            CancellationToken cancellationToken)
        {
            var amenityIds = requestedAmenityIds.Distinct().ToList();
            var validAmenityIds = await context.Amenities
                .Where(x =>
                    amenityIds.Contains(x.Id) &&
                    x.IsActive &&
                    (x.Scope == AmenityScope.Room || x.Scope == AmenityScope.Both))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (validAmenityIds.Count != amenityIds.Count)
            {
                throw new BadRequestException(
                    ErrorCodes.AmenityNotFound,
                    "Một hoặc nhiều mã tiện ích không hợp lệ.",
                    new { amenityIds });
            }

            return amenityIds;
        }

        private static void ValidateRoomFields(
            string roomNumber,
            int floor,
            decimal? areaM2,
            int maxOccupants)
        {
            if (string.IsNullOrWhiteSpace(roomNumber))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Số phòng là bắt buộc.",
                    new { field = nameof(roomNumber) });
            }

            if (floor < 0)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Tầng phải lớn hơn hoặc bằng 0.",
                    new { field = nameof(floor) });
            }

            if (areaM2 is <= 0)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Diện tích phải lớn hơn 0.",
                    new { field = nameof(areaM2) });
            }

            if (maxOccupants <= 0)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Số người tối đa phải lớn hơn 0.",
                    new { field = nameof(maxOccupants) });
            }
        }

        private static void ValidatePropertyImages(IReadOnlyCollection<UpdatePropertyImageItemRequest> images)
        {
            if (images.Count < 3)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Phòng cần có ít nhất 3 ảnh.",
                    new { field = nameof(images) });
            }

            var coverCount = images.Count(x => x.IsCover);

            if (coverCount != 1)
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Phòng cần có đúng 1 ảnh đại diện.",
                    new { field = nameof(images) });
            }

            if (images.Any(x => string.IsNullOrWhiteSpace(x.ObjectKey)))
            {
                throw new BadRequestException(
                    ErrorCodes.ValidationError,
                    "Mã lưu trữ ảnh là bắt buộc.",
                    new { field = nameof(images) });
            }
        }

        private static void ValidatePriceTiers(
            IReadOnlyCollection<RoomPriceTierRequest> priceTiers,
            int maxOccupants)
        {
            if (priceTiers.Count == 0)
            {
                throw new BadRequestException(
                    ErrorCodes.PriceTierInvalid,
                    "Cần có ít nhất 1 mức giá phòng.",
                    new { field = nameof(priceTiers) });
            }

            var duplicateOccupantCounts = priceTiers
                .GroupBy(x => x.OccupantCount)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToList();

            if (duplicateOccupantCounts.Count > 0)
            {
                throw new BadRequestException(
                    ErrorCodes.PriceTierInvalid,
                    "Không được trùng số người ở trong bảng giá.",
                    new { occupantCounts = duplicateOccupantCounts });
            }

            foreach (var tier in priceTiers)
            {
                ValidatePriceTier(tier.OccupantCount, tier.MonthlyRent, maxOccupants);
            }
        }

        private static void ValidatePriceTier(
            int occupantCount,
            decimal monthlyRent,
            int maxOccupants)
        {
            if (occupantCount <= 0 || occupantCount > maxOccupants)
            {
                throw new BadRequestException(
                    ErrorCodes.PriceTierInvalid,
                    "Số người áp dụng phải nằm trong khoảng từ 1 đến số người tối đa.",
                    new { occupantCount, maxOccupants });
            }

            if (monthlyRent <= 0)
            {
                throw new BadRequestException(
                    ErrorCodes.PriceTierInvalid,
                    "Giá thuê hằng tháng phải lớn hơn 0.",
                    new { monthlyRent });
            }
        }

        private static void ValidateRoomCanBeSubmitted(Room room)
        {
            if (!room.PriceTiers.Any(x => x.IsActive))
            {
                throw new BadRequestException(
                    ErrorCodes.PriceTierInvalid,
                    "Cần có ít nhất 1 mức giá đang áp dụng trước khi gửi hiển thị.",
                    new { roomId = room.Id });
            }

            foreach (var tier in room.PriceTiers)
            {
                ValidatePriceTier(tier.OccupantCount, tier.MonthlyRent, room.MaxOccupants);
            }
        }

        private async Task EnsurePriceTiersFitMaxOccupantsAsync(
            Guid roomId,
            int maxOccupants,
            CancellationToken cancellationToken)
        {
            var invalidTierExists = await context.RoomPriceTiers
                .AnyAsync(x => x.RoomId == roomId && x.OccupantCount > maxOccupants, cancellationToken);

            if (invalidTierExists)
            {
                throw new BadRequestException(
                    ErrorCodes.PriceTierInvalid,
                    "Bảng giá hiện tại có số người áp dụng vượt quá số người tối đa mới.",
                    new { maxOccupants });
            }
        }

        private static RoomResponse ToResponse(Room room)
        {
            return new RoomResponse
            {
                Id = room.Id,
                RoomingHouseId = room.RoomingHouseId,
                RoomNumber = room.RoomNumber,
                Floor = room.Floor,
                AreaM2 = room.AreaM2,
                MaxOccupants = room.MaxOccupants,
                Status = room.Status.ToString(),
                Description = room.Description,
                CreatedAt = room.CreatedAt,
                UpdatedAt = room.UpdatedAt,
                PriceTiers = room.PriceTiers
                    .OrderBy(x => x.OccupantCount)
                    .Select(x => new RoomPriceTierResponse
                    {
                        Id = x.Id,
                        OccupantCount = x.OccupantCount,
                        MonthlyRent = x.MonthlyRent,
                        IsActive = x.IsActive
                    })
                    .ToList(),
                Images = room.Images
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
                Amenities = room.RoomAmenities
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

        private static string BuildImageUrl(string objectKey)
        {
            return $"/uploads/{objectKey}";
        }
    }
}
