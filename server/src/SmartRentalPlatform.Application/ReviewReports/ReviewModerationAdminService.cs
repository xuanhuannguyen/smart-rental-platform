using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.RoomingHouses.Helpers;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.PropertyImages.Responses;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums.Notifications;
using SmartRentalPlatform.Domain.Enums.Properties;

namespace SmartRentalPlatform.Application.ReviewReports;

public sealed class ReviewModerationAdminService : IReviewModerationAdminService
{
    private readonly IAppDbContext context;
    private readonly INotificationService notificationService;

    public ReviewModerationAdminService(
        IAppDbContext context,
        INotificationService notificationService)
    {
        this.context = context;
        this.notificationService = notificationService;
    }

    public async Task<PagedResult<AdminReviewModerationItemResponse>> GetReviewsAsync(
        int page,
        int pageSize,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.RoomingHouseReviews
            .Include(x => x.RoomingHouse)
            .Include(x => x.TenantUser)
            .Include(x => x.Images)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<RoomingHouseReviewModerationStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(x => x.ModerationStatus == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.AiReviewedAt ?? x.UpdatedAt ?? x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminReviewModerationItemResponse>
        {
            Items = items.Select(Map).ToList(),
            TotalItems = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task ModerateAsync(
        Guid reviewId,
        Guid adminUserId,
        string action,
        string? adminNote,
        CancellationToken cancellationToken = default)
    {
        var review = await context.RoomingHouseReviews
            .Include(x => x.RoomingHouse)
            .FirstOrDefaultAsync(x => x.Id == reviewId, cancellationToken);

        if (review is null)
        {
            throw new NotFoundException(ErrorCodes.ReviewNotFound, "Không tìm thấy đánh giá.");
        }

        var normalizedAction = action.Trim();
        if (!normalizedAction.Equals("Approve", StringComparison.OrdinalIgnoreCase) &&
            !normalizedAction.Equals("Reject", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(ErrorCodes.ValidationError, "Hành động duyệt đánh giá không hợp lệ.");
        }

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        try
        {
            review.ReviewedByAdminId = adminUserId;
            review.AdminReviewedAt = DateTimeOffset.UtcNow;
            review.AdminNote = adminNote;

            if (normalizedAction.Equals("Approve", StringComparison.OrdinalIgnoreCase))
            {
                review.ModerationStatus = RoomingHouseReviewModerationStatus.Approved;
                review.ModerationReason = adminNote ?? review.ModerationReason ?? "Admin đã duyệt hiển thị đánh giá.";
                review.IsHidden = false;
            }
            else
            {
                review.ModerationStatus = RoomingHouseReviewModerationStatus.Rejected;
                review.ModerationReason = adminNote ?? review.ModerationReason ?? "Admin từ chối hiển thị đánh giá.";
                review.IsHidden = true;
            }

            await context.SaveChangesAsync(cancellationToken);
            await RoomingHouseRatingHelper.UpdateRatingAsync(context, review.RoomingHouseId, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            if (normalizedAction.Equals("Reject", StringComparison.OrdinalIgnoreCase))
            {
                await notificationService.CreateAsync(
                    review.TenantUserId,
                    NotificationType.RoomingHouseReviewRejected,
                    "Đánh giá bị từ chối",
                    $"Đánh giá của bạn về {review.RoomingHouse.Name} không được hiển thị. Lý do: {review.ModerationReason}",
                    review.RoomingHouseId.ToString(),
                    "RoomingHouse",
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static AdminReviewModerationItemResponse Map(RoomingHouseReview review)
        => new()
        {
            Id = review.Id,
            RoomingHouseId = review.RoomingHouseId,
            RoomingHouseName = review.RoomingHouse.Name,
            TenantUserId = review.TenantUserId,
            TenantDisplayName = review.TenantUser.DisplayName,
            TenantAvatarUrl = review.TenantUser.AvatarUrl,
            Rating = review.Rating,
            Comment = review.Comment,
            ModerationStatus = review.ModerationStatus.ToString(),
            ModerationReason = review.ModerationReason,
            AiModerationProvider = review.AiModerationProvider,
            AiModerationRiskLevel = review.AiModerationRiskLevel,
            AiModerationCategories = review.AiModerationCategories,
            AiContentComment = ExtractAiComment(review.AiModerationJson, "contentComment"),
            AiImageComment = ExtractAiComment(review.AiModerationJson, "imageComment"),
            AiReviewedAt = review.AiReviewedAt,
            AdminNote = review.AdminNote,
            CreatedAt = review.CreatedAt,
            UpdatedAt = review.UpdatedAt,
            Images = review.Images.OrderBy(x => x.SortOrder).Select(img => new PropertyImageResponse
            {
                Id = img.Id,
                ImageUrl = img.ImageUrl,
                Caption = img.Caption,
                IsCover = img.IsCover,
                SortOrder = img.SortOrder
            }).ToList()
        };

    private static string? ExtractAiComment(string? rawJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
