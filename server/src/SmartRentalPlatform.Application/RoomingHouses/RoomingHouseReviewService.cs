using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Common.Models;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.PropertyImages.Responses;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Requests;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
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

    public RoomingHouseReviewService(
        IAppDbContext context,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService,
        IReviewAiModerationService moderationService,
        INotificationService notificationService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
        _moderationService = moderationService;
        _notificationService = notificationService;
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
            .AnyAsync(x => x.RentalContractId == contractId && x.TenantUserId == tenantUserId, cancellationToken);

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
            .Where(x => eligibleContractIds.Contains(x.RentalContractId) && x.TenantUserId == tenantUserId)
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

            if (request.Images != null && request.Images.Any())
            {
                int sortOrder = 0;
                foreach (var file in request.Images)
                {
                    await using var stream = file.OpenReadStream();
                    var imageUploadFile = new ImageUploadFile { Content = stream, FileName = file.FileName, ContentType = file.ContentType, Length = file.Length };
                    var uploadResponse = await _fileStorageService.UploadImageAsync(imageUploadFile, FileUploadScope.RoomingHouse, cancellationToken);
                    
                    review.Images.Add(new PropertyImage
                    {
                        Id = Guid.NewGuid(),
                        RoomingHouseReviewId = review.Id,
                        ObjectKey = uploadResponse.ObjectKey,
                        ImageUrl = uploadResponse.Url,
                        SortOrder = sortOrder++,
                        CreatedAt = DateTimeOffset.UtcNow,
                        IsCover = false
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetReviewResponseAsync(review.Id, cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<RoomingHouseReviewListResponse> GetReviewsAsync(
        Guid roomingHouseId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RoomingHouseReviews
            .Include(x => x.TenantUser)
            .Include(x => x.Images)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Room)
            .Where(x => x.RoomingHouseId == roomingHouseId && !x.IsHidden && x.ModerationStatus == RoomingHouseReviewModerationStatus.Approved);

        var totalCount = await query.CountAsync(cancellationToken);
        
        var reviews = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var distribution = await _context.RoomingHouseReviews
            .Where(x => x.RoomingHouseId == roomingHouseId && !x.IsHidden && x.ModerationStatus == RoomingHouseReviewModerationStatus.Approved)
            .GroupBy(x => x.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Rating, x => x.Count, cancellationToken);

        var house = await _context.RoomingHouses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == roomingHouseId, cancellationToken);

        var reviewIds = reviews.Select(x => x.Id).ToList();
        var reportedReviewIds = await _context.ReviewReports
            .Where(x => reviewIds.Contains(x.RoomingHouseReviewId))
            .Select(x => x.RoomingHouseReviewId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var response = new RoomingHouseReviewListResponse
        {
            AverageRating = house?.AverageRating ?? 0,
            TotalReviews = house?.TotalReviews ?? 0,
            RatingDistribution = distribution,
            Reviews = reviews.Select(r => 
            {
                var mapped = MapToResponse(r);
                mapped.IsReported = reportedReviewIds.Contains(r.Id);
                return mapped;
            }).ToList()
        };

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

            if (request.NewImages != null && request.NewImages.Any())
            {
                int sortOrder = review.Images.Count > 0 ? review.Images.Max(x => x.SortOrder) + 1 : 0;
                foreach (var file in request.NewImages)
                {
                    await using var stream = file.OpenReadStream();
                    var imageUploadFile = new ImageUploadFile { Content = stream, FileName = file.FileName, ContentType = file.ContentType, Length = file.Length };
                    var uploadResponse = await _fileStorageService.UploadImageAsync(imageUploadFile, FileUploadScope.RoomingHouse, cancellationToken);

                    var newImage = new PropertyImage
                    {
                        Id = Guid.NewGuid(),
                        RoomingHouseReviewId = review.Id,
                        ObjectKey = uploadResponse.ObjectKey,
                        ImageUrl = uploadResponse.Url,
                        SortOrder = sortOrder++,
                        CreatedAt = DateTimeOffset.UtcNow,
                        IsCover = false
                    };
                    
                    _context.PropertyImages.Add(newImage);
                    review.Images.Add(newImage);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

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
            Images = review.Images.OrderBy(x => x.SortOrder).Select(img => new PropertyImageResponse
            {
                Id = img.Id,
                ImageUrl = img.ImageUrl,
                Caption = img.Caption,
                IsCover = img.IsCover,
                SortOrder = img.SortOrder
            }).ToList()
        };
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
