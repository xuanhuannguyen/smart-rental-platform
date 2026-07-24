using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Media;
using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.PropertyImages.Responses;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Requests;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;
using SmartRentalPlatform.Domain.Entities.Media;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.Media;
using SmartRentalPlatform.Domain.Enums.RentalContracts;
using SmartRentalPlatform.Contracts.Files;
using SmartRentalPlatform.Application.RoomingHouses.Helpers;
using SmartRentalPlatform.Application.RoomingHouses.ReviewModeration;
using SmartRentalPlatform.Domain.Enums.Notifications;
using SmartRentalPlatform.Domain.Enums.Properties;

namespace SmartRentalPlatform.Application.RoomingHouses;

public class RoomingHouseReviewService : IRoomingHouseReviewService
{
    private const int MaxReviewImages = 4;

    private readonly IAppDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IReviewAiModerationService _moderationService;
    private readonly INotificationService _notificationService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RoomingHouseReviewService> _logger;

    public RoomingHouseReviewService(
        IAppDbContext context,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService,
        IReviewAiModerationService moderationService,
        INotificationService notificationService,
        IMemoryCache memoryCache,
        ILogger<RoomingHouseReviewService> logger)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
        _moderationService = moderationService;
        _notificationService = notificationService;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<ReviewEligibilityResponse> CheckEligibilityAsync(
        Guid contractId,
        Guid tenantUserId,
        CancellationToken cancellationToken = default)
    {
        var contract = await _context.RentalContracts
            .Include(x => x.Occupants)
            .FirstOrDefaultAsync(x => x.Id == contractId, cancellationToken);

        if (contract == null)
            throw new NotFoundException(ErrorCodes.RentalContractNotFound, "Không tìm thấy hợp đồng.");

        // Phải là người thuê chính hoặc occupant trong hợp đồng
        var isMainTenant = contract.MainTenantUserId == tenantUserId;
        var occupant = contract.Occupants.FirstOrDefault(x => x.UserId == tenantUserId);
        if (!isMainTenant && occupant == null)
            throw new ForbiddenException(ErrorCodes.ReviewForbidden, "Bạn không có quyền đánh giá hợp đồng này.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Contract must have started.
        if (contract.StartDate > today)
        {
            return new ReviewEligibilityResponse
            {
                IsEligible = false,
                Reason = "Hợp đồng chưa bắt đầu."
            };
        }

        if (contract.Status == RentalContractStatus.Rejected)
        {
            return new ReviewEligibilityResponse
            {
                IsEligible = false,
                Reason = "Hợp đồng không đủ điều kiện để đánh giá."
            };
        }

        var userHasStayed = HasReviewableStay(contract, occupant, isMainTenant, today);

        if (!IsReviewableContractStatus(contract.Status, userHasStayed))
        {
            return new ReviewEligibilityResponse
            {
                IsEligible = false,
                Reason = "Chỉ có thể đánh giá hợp đồng đang thuê hoặc đã kết thúc."
            };
        }

        if (!userHasStayed)
        {
            return new ReviewEligibilityResponse
            {
                IsEligible = false,
                Reason = "Bạn cần đã dọn vào khu trọ trước khi đánh giá."
            };
        }

        // Đã review chưa?
        var existingReview = await _context.RoomingHouseReviews
            .AnyAsync(
                x => x.RentalContractId == contractId &&
                     x.TenantUserId == tenantUserId &&
                     !x.IsHidden,
                cancellationToken);

        if (existingReview)
        {
            return new ReviewEligibilityResponse
            {
                IsEligible = false,
                Reason = "Bạn đã đánh giá hợp đồng này rồi."
            };
        }

        return new ReviewEligibilityResponse
        {
            IsEligible = true,
            Reason = null
        };
    }

    public async Task<RoomingHouseReviewEligibilitySummaryResponse> CheckRoomingHouseEligibilityAsync(
        Guid roomingHouseId,
        Guid tenantUserId,
        CancellationToken cancellationToken = default)
    {
        // Find all contracts for this tenant in this rooming house
        var contracts = await _context.RentalContracts
            .Include(x => x.Room)
            .Include(x => x.Occupants)
            .Where(x => x.Room.RoomingHouseId == roomingHouseId &&
                        (x.MainTenantUserId == tenantUserId || x.Occupants.Any(o => o.UserId == tenantUserId)))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Find contracts that are eligible (started, active/expired, or cancelled after an actual stay)
        var eligibleContracts = contracts.Where(contract =>
        {
            var occupant = contract.Occupants.FirstOrDefault(o => o.UserId == tenantUserId);
            var isMainTenant = contract.MainTenantUserId == tenantUserId;
            var userHasStayed = HasReviewableStay(contract, occupant, isMainTenant, today);

            return contract.StartDate <= today &&
                   IsReviewableContractStatus(contract.Status, userHasStayed) &&
                   userHasStayed;
        }).ToList();

        if (!eligibleContracts.Any())
        {
            return new RoomingHouseReviewEligibilitySummaryResponse
            {
                IsEligible = false,
                ContractId = null,
                Reason = "Bạn cần có hợp đồng đang thuê hoặc đã kết thúc tại khu trọ này để viết đánh giá.",
                ExistingReview = null,
                ReviewableContracts = new List<ReviewableContractResponse>()
            };
        }

        var eligibleContractIds = eligibleContracts.Select(x => x.Id).ToList();
        var existingReviews = await _context.RoomingHouseReviews
            .Where(
                x => eligibleContractIds.Contains(x.RentalContractId) &&
                     x.TenantUserId == tenantUserId &&
                     !x.IsHidden)
            .ToListAsync(cancellationToken);

        var reviewedContractIds = existingReviews.Select(x => x.RentalContractId).ToHashSet();
        var eligibleContract = eligibleContracts.FirstOrDefault(x => !reviewedContractIds.Contains(x.Id));
        var reviewByContractId = new Dictionary<Guid, RoomingHouseReviewResponse>();
        foreach (var review in existingReviews)
        {
            reviewByContractId[review.RentalContractId] = await GetReviewResponseAsync(review.Id, cancellationToken);
        }

        var reviewableContracts = eligibleContracts.Select(contract =>
        {
            reviewByContractId.TryGetValue(contract.Id, out var review);
            return new ReviewableContractResponse
            {
                ContractId = contract.Id,
                RoomNumber = contract.Room.RoomNumber,
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                Status = contract.Status.ToString(),
                CanReview = review == null,
                ReviewStatus = review?.ModerationStatus,
                ReviewId = review?.Id,
                Review = review
            };
        }).ToList();

        if (eligibleContract != null)
        {
            return new RoomingHouseReviewEligibilitySummaryResponse
            {
                IsEligible = true,
                ContractId = eligibleContract.Id,
                Reason = null,
                ExistingReview = null,
                ReviewableContracts = reviewableContracts
            };
        }

        // All eligible contracts already have reviews.
        var existingReview = existingReviews
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (existingReview != null)
        {
            var existingReviewResponse = await GetReviewResponseAsync(existingReview.Id, cancellationToken);
            return new RoomingHouseReviewEligibilitySummaryResponse
            {
                IsEligible = false,
                ContractId = existingReview.RentalContractId,
                Reason = "Bạn đã đánh giá tất cả hợp đồng đủ điều kiện tại khu trọ này.",
                ExistingReview = existingReviewResponse,
                ReviewableContracts = reviewableContracts
            };
        }

        return new RoomingHouseReviewEligibilitySummaryResponse
        {
            IsEligible = false,
            ContractId = null,
            Reason = "Bạn cần có hợp đồng đang thuê hoặc đã kết thúc tại khu trọ này để viết đánh giá.",
            ExistingReview = null,
            ReviewableContracts = reviewableContracts
        };
    }

    public async Task<RoomingHouseReviewResponse> CreateReviewAsync(
        Guid contractId,
        Guid tenantUserId,
        CreateRoomingHouseReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var eligibility = await CheckEligibilityAsync(contractId, tenantUserId, cancellationToken);
        if (!eligibility.IsEligible)
            throw new ConflictException(ErrorCodes.ReviewNotEligible, eligibility.Reason!);

        if (request.Images.Count > MaxReviewImages)
            throw new ConflictException(ErrorCodes.ValidationError, $"Mỗi đánh giá chỉ được tải tối đa {MaxReviewImages} ảnh.");

        var contract = await _context.RentalContracts.Include(x => x.Room).FirstOrDefaultAsync(x => x.Id == contractId, cancellationToken);
        var uploadedImageAssetIds = new List<Guid>();

        if (request.Images != null && request.Images.Any())
        {
            foreach (var file in request.Images)
            {
                await using var stream = file.OpenReadStream();
                var imageUploadFile = new ImageUploadFile { Content = stream, FileName = file.FileName, ContentType = file.ContentType, Length = file.Length };
                var uploadResponse = await _fileStorageService.UploadImageAsync(imageUploadFile, FileUploadScope.RoomingHouse, cancellationToken);
                if (!uploadResponse.MediaAssetId.HasValue)
                {
                    throw new InvalidOperationException("Review image upload did not return mediaAssetId.");
                }

                uploadedImageAssetIds.Add(uploadResponse.MediaAssetId.Value);
            }
        }

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            var review = new RoomingHouseReview
            {
                Id = Guid.NewGuid(),
                RoomingHouseId = contract!.Room.RoomingHouseId,
                TenantUserId = tenantUserId,
                RentalContractId = contractId,
                Rating = request.Rating,
                Comment = request.Comment,
                CreatedAt = DateTimeOffset.UtcNow,
                IsHidden = false,
                ModerationStatus = RoomingHouseReviewModerationStatus.PendingAiReview,
                ModerationReason = "Đánh giá đang chờ AI kiểm duyệt."
            };

            _context.RoomingHouseReviews.Add(review);

            if (uploadedImageAssetIds.Count > 0)
            {
                int sortOrder = 0;
                foreach (var mediaAssetId in uploadedImageAssetIds)
                {
                    var propertyImageId = Guid.NewGuid();
                    var createdAt = DateTimeOffset.UtcNow;
                    await LinkReviewImageMediaAssetAsync(
                        propertyImageId,
                        tenantUserId,
                        mediaAssetId,
                        createdAt,
                        cancellationToken);

                    review.Images.Add(new PropertyImage
                    {
                        Id = propertyImageId,
                        RoomingHouseReviewId = review.Id,
                        MediaAssetId = mediaAssetId,
                        ImageUrl = BuildReviewImageUrl(mediaAssetId),
                        SortOrder = sortOrder++,
                        CreatedAt = createdAt,
                        IsCover = false
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            InvalidatePublicReviewCache(review.RoomingHouseId);

            return await GetReviewResponseAsync(review.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(
                ex,
                "Failed to create rooming house review. ContractId={ContractId}, TenantUserId={TenantUserId}, Rating={Rating}, ImageCount={ImageCount}",
                contractId,
                tenantUserId,
                request.Rating,
                request.Images?.Count ?? 0);
            throw;
        }
    }

    public async Task<RoomingHouseReviewListResponse> GetReviewsAsync(
        Guid roomingHouseId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var cacheVersion = GetPublicReviewCacheVersion(roomingHouseId);
        var cacheKey = $"public-rooming-house-reviews:{roomingHouseId:N}:{cacheVersion}:{page}:{pageSize}";
        if (_memoryCache.TryGetValue(cacheKey, out RoomingHouseReviewListResponse? cachedResponse) &&
            cachedResponse is not null)
        {
            return cachedResponse;
        }

        var baseQuery = _context.RoomingHouseReviews
            .AsNoTracking()
            .Where(x => x.RoomingHouseId == roomingHouseId &&
                        !x.IsHidden &&
                        x.ModerationStatus == RoomingHouseReviewModerationStatus.Approved);

        var reviews = await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RoomingHouseReviewResponse
            {
                Id = x.Id,
                RentalContractId = x.RentalContractId,
                RoomNumber = x.RentalContract.Room.RoomNumber,
                ContractStartDate = x.RentalContract.StartDate,
                ContractEndDate = x.RentalContract.EndDate,
                TenantUserId = x.TenantUserId,
                TenantDisplayName = x.TenantUser.DisplayName,
                TenantAvatarUrl = x.TenantUser.AvatarUrl,
                Rating = x.Rating,
                Comment = x.Comment,
                LandlordReply = x.LandlordReply,
                LandlordReplyCreatedAt = x.LandlordReplyCreatedAt,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                IsReported = _context.ReviewReports.Any(report => report.RoomingHouseReviewId == x.Id),
                ModerationStatus = x.ModerationStatus.ToString(),
                ModerationReason = x.ModerationReason,
                AiModerationProvider = x.AiModerationProvider,
                AiModerationRiskLevel = x.AiModerationRiskLevel,
                AdminNote = x.AdminNote,
                Images = x.Images
                    .Where(image => image.MediaAssetId.HasValue)
                    .OrderBy(image => image.SortOrder)
                    .Select(image => new PropertyImageResponse
                    {
                        Id = image.Id,
                        MediaAssetId = image.MediaAssetId,
                        Caption = image.Caption,
                        IsCover = image.IsCover,
                        SortOrder = image.SortOrder,
                        CreatedAt = image.CreatedAt
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        foreach (var review in reviews)
        {
            foreach (var image in review.Images)
            {
                image.ImageUrl = image.MediaAssetId.HasValue
                    ? BuildReviewImageUrl(image.MediaAssetId.Value)
                    : string.Empty;
            }
        }

        var distribution = await baseQuery
            .GroupBy(x => x.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Rating, x => x.Count, cancellationToken);

        var house = await _context.RoomingHouses
            .AsNoTracking()
            .Where(x => x.Id == roomingHouseId)
            .Select(x => new { x.AverageRating, x.TotalReviews })
            .FirstOrDefaultAsync(cancellationToken);

        var response = new RoomingHouseReviewListResponse
        {
            AverageRating = house?.AverageRating ?? 0,
            TotalReviews = house?.TotalReviews ?? 0,
            RatingDistribution = distribution,
            Reviews = reviews
        };

        _memoryCache.Set(
            cacheKey,
            response,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                SlidingExpiration = TimeSpan.FromMinutes(1)
            });

        return response;
    }

    public async Task<RoomingHouseReviewResponse> UpdateReviewAsync(
        Guid reviewId,
        Guid tenantUserId,
        UpdateRoomingHouseReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var review = await _context.RoomingHouseReviews
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == reviewId, cancellationToken);

        if (review == null)
            throw new NotFoundException(ErrorCodes.ReviewNotFound, "Không tìm thấy đánh giá.");

        if (review.TenantUserId != tenantUserId)
            throw new ForbiddenException(ErrorCodes.ReviewForbidden, "Bạn không có quyền sửa đánh giá này.");

        if (DateTimeOffset.UtcNow > review.CreatedAt.AddDays(7))
            throw new ConflictException(ErrorCodes.ValidationError, "Chỉ được sửa đánh giá trong vòng 7 ngày.");

        var totalImagesAfterUpdate = request.RetainedImageIds.Count + request.NewImages.Count;
        if (totalImagesAfterUpdate > MaxReviewImages)
            throw new ConflictException(ErrorCodes.ValidationError, $"Mỗi đánh giá chỉ được giữ tối đa {MaxReviewImages} ảnh.");

        var uploadedImageAssetIds = new List<Guid>();
        if (request.NewImages != null && request.NewImages.Any())
        {
            foreach (var file in request.NewImages)
            {
                await using var stream = file.OpenReadStream();
                var imageUploadFile = new ImageUploadFile { Content = stream, FileName = file.FileName, ContentType = file.ContentType, Length = file.Length };
                var uploadResponse = await _fileStorageService.UploadImageAsync(imageUploadFile, FileUploadScope.RoomingHouse, cancellationToken);
                if (!uploadResponse.MediaAssetId.HasValue)
                {
                    throw new InvalidOperationException("Review image upload did not return mediaAssetId.");
                }

                uploadedImageAssetIds.Add(uploadResponse.MediaAssetId.Value);
            }
        }

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            review.Rating = request.Rating;
            review.Comment = request.Comment;
            review.UpdatedAt = DateTimeOffset.UtcNow;
            review.ModerationStatus = RoomingHouseReviewModerationStatus.PendingAiReview;
            review.ModerationReason = "Đánh giá đã được chỉnh sửa và đang chờ AI kiểm duyệt lại.";
            review.AiModerationProvider = null;
            review.AiModerationRiskLevel = null;
            review.AiModerationCategories = null;
            review.AiModerationJson = null;
            review.AiReviewedAt = null;
            review.ReviewedByAdminId = null;
            review.AdminReviewedAt = null;
            review.AdminNote = null;

            var imagesToRemove = review.Images
                .Where(x => request.RetainedImageIds == null || !request.RetainedImageIds.Contains(x.Id))
                .ToList();

            foreach (var img in imagesToRemove)
            {
                review.Images.Remove(img);
                _context.PropertyImages.Remove(img);
            }

            await UnlinkReviewImageMediaAssetsAsync(imagesToRemove, DateTimeOffset.UtcNow, cancellationToken);

            if (uploadedImageAssetIds.Count > 0)
            {
                int sortOrder = review.Images.Count > 0 ? review.Images.Max(x => x.SortOrder) + 1 : 0;
                foreach (var mediaAssetId in uploadedImageAssetIds)
                {
                    var propertyImageId = Guid.NewGuid();
                    var createdAt = DateTimeOffset.UtcNow;
                    await LinkReviewImageMediaAssetAsync(
                        propertyImageId,
                        tenantUserId,
                        mediaAssetId,
                        createdAt,
                        cancellationToken);

                    var newImage = new PropertyImage
                    {
                        Id = propertyImageId,
                        RoomingHouseReviewId = review.Id,
                        MediaAssetId = mediaAssetId,
                        ImageUrl = BuildReviewImageUrl(mediaAssetId),
                        SortOrder = sortOrder++,
                        CreatedAt = createdAt,
                        IsCover = false
                    };

                    _context.PropertyImages.Add(newImage);
                    review.Images.Add(newImage);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            InvalidatePublicReviewCache(review.RoomingHouseId);

            return await GetReviewResponseAsync(review.Id, cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteReviewAsync(
        Guid reviewId,
        Guid tenantUserId,
        CancellationToken cancellationToken = default)
    {
        var review = await _context.RoomingHouseReviews
            .FirstOrDefaultAsync(x => x.Id == reviewId, cancellationToken);

        if (review == null)
            throw new NotFoundException(ErrorCodes.ReviewNotFound, "Không tìm thấy đánh giá.");

        if (review.TenantUserId != tenantUserId)
            throw new ForbiddenException(ErrorCodes.ReviewForbidden, "Bạn không có quyền xóa đánh giá này.");

        if (DateTimeOffset.UtcNow > review.CreatedAt.AddDays(7))
            throw new ConflictException(ErrorCodes.ValidationError, "Chỉ được xóa đánh giá trong vòng 7 ngày.");

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            review.IsHidden = true;
            review.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await RoomingHouseRatingHelper.UpdateRatingAsync(_context, review.RoomingHouseId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            InvalidatePublicReviewCache(review.RoomingHouseId);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ReplyReviewAsync(
        Guid reviewId,
        Guid landlordUserId,
        ReplyRoomingHouseReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var review = await _context.RoomingHouseReviews
            .Include(x => x.RoomingHouse)
            .FirstOrDefaultAsync(x => x.Id == reviewId, cancellationToken);

        if (review == null)
            throw new NotFoundException(ErrorCodes.ReviewNotFound, "Không tìm thấy đánh giá.");

        if (review.RoomingHouse.LandlordUserId != landlordUserId)
            throw new ForbiddenException(ErrorCodes.ReviewForbidden, "Bạn không có quyền phản hồi đánh giá này.");

        review.LandlordReply = request.Reply;
        review.LandlordReplyCreatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        InvalidatePublicReviewCache(review.RoomingHouseId);

        await _notificationService.CreateAsync(
            review.TenantUserId,
            NotificationType.RoomingHouseReviewReplied,
            "Chủ trọ đã phản hồi đánh giá",
            $"Chủ trọ {review.RoomingHouse.Name} đã phản hồi đánh giá của bạn.",
            review.RoomingHouseId.ToString(),
            "RoomingHouse",
            cancellationToken);
    }

    public async Task DeleteReplyAsync(
        Guid reviewId,
        Guid landlordUserId,
        CancellationToken cancellationToken = default)
    {
        var review = await _context.RoomingHouseReviews
            .Include(x => x.RoomingHouse)
            .FirstOrDefaultAsync(x => x.Id == reviewId, cancellationToken);

        if (review == null)
            throw new NotFoundException(ErrorCodes.ReviewNotFound, "Không tìm thấy đánh giá.");

        if (review.RoomingHouse.LandlordUserId != landlordUserId)
            throw new ForbiddenException(ErrorCodes.ReviewForbidden, "Bạn không có quyền xóa phản hồi đánh giá này.");

        review.LandlordReply = null;
        review.LandlordReplyCreatedAt = null;

        await _context.SaveChangesAsync(cancellationToken);
        InvalidatePublicReviewCache(review.RoomingHouseId);
    }

    private long GetPublicReviewCacheVersion(Guid roomingHouseId)
    {
        return _memoryCache.GetOrCreate(
            $"public-rooming-house-reviews-version:{roomingHouseId:N}",
            entry =>
            {
                entry.Priority = CacheItemPriority.NeverRemove;
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            });
    }

    private void InvalidatePublicReviewCache(Guid roomingHouseId)
    {
        _memoryCache.Set(
            $"public-rooming-house-reviews-version:{roomingHouseId:N}",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
    }



    private async Task<RoomingHouseReviewResponse> GetReviewResponseAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var review = await _context.RoomingHouseReviews
            .Include(x => x.TenantUser)
            .Include(x => x.Images)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Room)
            .FirstOrDefaultAsync(x => x.Id == reviewId, cancellationToken);

        if (review == null)
            throw new NotFoundException(ErrorCodes.ReviewNotFound, "Không tìm thấy đánh giá.");

        var response = MapToResponse(review);
        response.IsReported = await _context.ReviewReports.AnyAsync(x => x.RoomingHouseReviewId == reviewId, cancellationToken);

        return response;
    }

    private RoomingHouseReviewResponse MapToResponse(RoomingHouseReview review)
    {
        return new RoomingHouseReviewResponse
        {
            Id = review.Id,
            RentalContractId = review.RentalContractId,
            RoomNumber = review.RentalContract?.Room?.RoomNumber,
            ContractStartDate = review.RentalContract?.StartDate,
            ContractEndDate = review.RentalContract?.EndDate,
            TenantUserId = review.TenantUserId,
            TenantDisplayName = review.TenantUser.DisplayName,
            TenantAvatarUrl = review.TenantUser.AvatarUrl,
            Rating = review.Rating,
            Comment = review.Comment,
            LandlordReply = review.LandlordReply,
            LandlordReplyCreatedAt = review.LandlordReplyCreatedAt,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt,
            ModerationStatus = review.ModerationStatus.ToString(),
            ModerationReason = review.ModerationReason,
            AiModerationProvider = review.AiModerationProvider,
            AiModerationRiskLevel = review.AiModerationRiskLevel,
            AdminNote = review.AdminNote,
            Images = review.Images
                .Where(x => x.MediaAssetId.HasValue)
                .OrderBy(x => x.SortOrder)
                .Select(img => new PropertyImageResponse
            {
                Id = img.Id,
                MediaAssetId = img.MediaAssetId,
                ImageUrl = BuildReviewImageUrl(img.MediaAssetId!.Value),
                Caption = img.Caption,
                IsCover = img.IsCover,
                SortOrder = img.SortOrder,
                CreatedAt = img.CreatedAt
            }).ToList()
        };
    }

    private async Task LinkReviewImageMediaAssetAsync(
        Guid propertyImageId,
        Guid ownerUserId,
        Guid mediaAssetId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mediaAsset = await _context.MediaAssets
            .FirstOrDefaultAsync(x => x.Id == mediaAssetId, cancellationToken)
            ?? throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset ảnh đánh giá không tồn tại.",
                new { mediaAssetId });

        if (mediaAsset.OwnerUserId.HasValue && mediaAsset.OwnerUserId.Value != ownerUserId)
        {
            throw new BadRequestException(
                ErrorCodes.ImageInvalidOwner,
                "Bạn không có quyền sử dụng media asset ảnh đánh giá này.",
                new { mediaAssetId });
        }

        if (mediaAsset.Scope != MediaScope.RoomingHouseImage || mediaAsset.Visibility != MediaVisibility.Public)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset không phù hợp với ảnh đánh giá.",
                new { mediaAssetId, expectedScope = MediaScope.RoomingHouseImage.ToString() });
        }

        if (mediaAsset.Status is MediaStatus.PendingUpload or MediaStatus.Deleted)
        {
            throw new BadRequestException(
                ErrorCodes.ValidationError,
                "Media asset ảnh đánh giá chưa sẵn sàng để liên kết.",
                new { mediaAssetId, status = mediaAsset.Status.ToString() });
        }

        mediaAsset.OwnerUserId = ownerUserId;
        mediaAsset.Scope = MediaScope.RoomingHouseImage;
        mediaAsset.Visibility = MediaVisibility.Public;
        mediaAsset.Status = MediaStatus.Linked;
        mediaAsset.LinkedEntityType = nameof(PropertyImage);
        mediaAsset.LinkedEntityId = propertyImageId;
        mediaAsset.DeletedAt = null;
        mediaAsset.UpdatedAt = now;
    }

    private async Task UnlinkReviewImageMediaAssetsAsync(
        IEnumerable<PropertyImage> images,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mediaAssetIds = images
            .Where(x => x.MediaAssetId.HasValue)
            .Select(x => x.MediaAssetId!.Value)
            .Distinct()
            .ToList();

        if (mediaAssetIds.Count == 0)
        {
            return;
        }

        var mediaAssets = await _context.MediaAssets
            .Where(x => mediaAssetIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var mediaAsset in mediaAssets)
        {
            mediaAsset.LinkedEntityType = null;
            mediaAsset.LinkedEntityId = null;
            mediaAsset.Status = MediaStatus.Deleted;
            mediaAsset.DeletedAt = now;
            mediaAsset.UpdatedAt = now;
        }
    }

    private static string BuildReviewImageUrl(Guid mediaAssetId)
    {
        return PublicMediaPathBuilder.Build(mediaAssetId);
    }

    private static bool IsReviewableContractStatus(RentalContractStatus status, bool userHasStayed)
    {
        return status is RentalContractStatus.Active or RentalContractStatus.Expired ||
               (status == RentalContractStatus.Cancelled && userHasStayed);
    }

    private static bool IsReviewableOccupantStatus(ContractOccupantStatus status)
    {
        return status is ContractOccupantStatus.Active or ContractOccupantStatus.MoveOut;
    }

    private static bool HasReviewableStay(
        RentalContract contract,
        ContractOccupant? occupant,
        bool isMainTenant,
        DateOnly today)
    {
        if (occupant != null)
        {
            return IsReviewableOccupantStatus(occupant.Status) && occupant.MoveInDate <= today;
        }

        return isMainTenant && contract.ActivatedAt != null && contract.StartDate <= today;
    }
}
