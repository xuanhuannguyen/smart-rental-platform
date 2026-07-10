using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.ReviewReports.Requests;
using SmartRentalPlatform.Contracts.ReviewReports.Responses;
using SmartRentalPlatform.Domain.Entities.Properties;
using SmartRentalPlatform.Domain.Enums;
using SmartRentalPlatform.Contracts.PropertyImages.Responses;
using SmartRentalPlatform.Contracts.RoomingHouseReviews.Responses;
using SmartRentalPlatform.Application.RoomingHouses.Helpers;

namespace SmartRentalPlatform.Application.ReviewReports;

public class ReviewReportService : IReviewReportService
{
    private readonly IAppDbContext _context;

    public ReviewReportService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task CreateReportAsync(
        Guid reviewId,
        Guid landlordUserId,
        CreateReviewReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var review = await _context.RoomingHouseReviews
            .Include(x => x.RoomingHouse)
            .FirstOrDefaultAsync(x => x.Id == reviewId, cancellationToken);

        if (review == null)
            throw new NotFoundException(ErrorCodes.ReviewNotFound, "Không tìm thấy đánh giá.");

        // Chỉ chủ trọ của khu trọ đó mới có quyền report
        if (review.RoomingHouse.LandlordUserId != landlordUserId)
            throw new ForbiddenException(ErrorCodes.ReviewForbidden, "Bạn không có quyền báo cáo đánh giá này.");

        var existingReport = await _context.ReviewReports
            .AnyAsync(x => x.RoomingHouseReviewId == reviewId && x.ReporterUserId == landlordUserId, cancellationToken);

        if (existingReport)
            throw new ConflictException(ErrorCodes.ReportAlreadyExists, "Bạn đã báo cáo đánh giá này rồi.");

        var report = new ReviewReport
        {
            Id = Guid.NewGuid(),
            RoomingHouseReviewId = reviewId,
            ReporterUserId = landlordUserId,
            Reason = request.Reason,
            Status = ReportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.ReviewReports.Add(report);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<ReviewReportResponse>> GetReportsAsync(
        int page,
        int pageSize,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ReviewReports
            .Include(x => x.ReporterUser)
            .Include(x => x.RoomingHouseReview)
                .ThenInclude(r => r.TenantUser)
            .Include(x => x.RoomingHouseReview)
                .ThenInclude(r => r.Images)
            .Include(x => x.RoomingHouseReview)
                .ThenInclude(r => r.RoomingHouse)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            if (status.Equals("Processed", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Status != ReportStatus.Pending);
            }
            else if (Enum.TryParse<ReportStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(x => x.Status == parsedStatus);
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var reports = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var responseList = reports.Select(report => new ReviewReportResponse
        {
            Id = report.Id,
            RoomingHouseReviewId = report.RoomingHouseReviewId,
            ReporterUserId = report.ReporterUserId,
            ReporterDisplayName = report.ReporterUser.DisplayName,
            Reason = report.Reason,
            Status = report.Status.ToString(),
            AdminNote = report.AdminNote,
            CreatedAt = report.CreatedAt,
            ResolvedAt = report.ResolvedAt,
            RoomingHouseName = report.RoomingHouseReview != null && report.RoomingHouseReview.RoomingHouse != null ? report.RoomingHouseReview.RoomingHouse.Name : string.Empty,
            Review = report.RoomingHouseReview == null ? null : new RoomingHouseReviewResponse
            {
                Id = report.RoomingHouseReview.Id,
                TenantUserId = report.RoomingHouseReview.TenantUserId,
                TenantDisplayName = report.RoomingHouseReview.TenantUser.DisplayName,
                TenantAvatarUrl = report.RoomingHouseReview.TenantUser.AvatarUrl,
                Rating = report.RoomingHouseReview.Rating,
                Comment = report.RoomingHouseReview.Comment,
                LandlordReply = report.RoomingHouseReview.LandlordReply,
                LandlordReplyCreatedAt = report.RoomingHouseReview.LandlordReplyCreatedAt,
                CreatedAt = report.RoomingHouseReview.CreatedAt,
                UpdatedAt = report.RoomingHouseReview.UpdatedAt,
                Images = report.RoomingHouseReview.Images.OrderBy(img => img.SortOrder).Select(img => new PropertyImageResponse
                {
                    Id = img.Id,
                    ImageUrl = img.ImageUrl,
                    Caption = img.Caption,
                    IsCover = img.IsCover,
                    SortOrder = img.SortOrder
                }).ToList()
            }
        }).ToList();

        return new PagedResult<ReviewReportResponse>
        {
            Items = responseList,
            TotalItems = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<ReviewReportResponse> GetReportDetailAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await _context.ReviewReports
            .Include(r => r.ReporterUser)
            .Include(r => r.RoomingHouseReview)
                .ThenInclude(rev => rev.TenantUser)
            .Include(r => r.RoomingHouseReview)
                .ThenInclude(rev => rev.Images)
            .Include(r => r.RoomingHouseReview)
                .ThenInclude(rev => rev.RoomingHouse)
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

        if (report == null)
            throw new Exception("Không tìm thấy báo cáo."); // You might want to use a specific NotFoundException

        return new ReviewReportResponse
        {
            Id = report.Id,
            RoomingHouseReviewId = report.RoomingHouseReviewId,
            ReporterUserId = report.ReporterUserId,
            ReporterDisplayName = report.ReporterUser.DisplayName,
            Reason = report.Reason,
            Status = report.Status.ToString(),
            AdminNote = report.AdminNote,
            CreatedAt = report.CreatedAt,
            ResolvedAt = report.ResolvedAt,
            RoomingHouseName = report.RoomingHouseReview != null && report.RoomingHouseReview.RoomingHouse != null ? report.RoomingHouseReview.RoomingHouse.Name : string.Empty,
            Review = report.RoomingHouseReview == null ? null : new RoomingHouseReviewResponse
            {
                Id = report.RoomingHouseReview.Id,
                TenantUserId = report.RoomingHouseReview.TenantUserId,
                TenantDisplayName = report.RoomingHouseReview.TenantUser.DisplayName,
                TenantAvatarUrl = report.RoomingHouseReview.TenantUser.AvatarUrl,
                Rating = report.RoomingHouseReview.Rating,
                Comment = report.RoomingHouseReview.Comment,
                LandlordReply = report.RoomingHouseReview.LandlordReply,
                LandlordReplyCreatedAt = report.RoomingHouseReview.LandlordReplyCreatedAt,
                CreatedAt = report.RoomingHouseReview.CreatedAt,
                UpdatedAt = report.RoomingHouseReview.UpdatedAt,
                Images = report.RoomingHouseReview.Images.OrderBy(img => img.SortOrder).Select(img => new PropertyImageResponse
                {
                    Id = img.Id,
                    ImageUrl = img.ImageUrl,
                    Caption = img.Caption,
                    IsCover = img.IsCover,
                    SortOrder = img.SortOrder
                }).ToList()
            }
        };
    }

    public async Task ResolveReportAsync(
        Guid reportId,
        Guid adminUserId,
        bool hideReview,
        string? adminNote,
        CancellationToken cancellationToken = default)
    {
        var report = await _context.ReviewReports
            .Include(x => x.RoomingHouseReview)
            .FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);

        if (report == null)
            throw new NotFoundException(ErrorCodes.ReportNotFound, "Không tìm thấy báo cáo.");

        if (report.Status != ReportStatus.Pending)
            throw new ConflictException(ErrorCodes.InvalidStatus, "Báo cáo đã được xử lý.");

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            report.Status = hideReview ? ReportStatus.Resolved : ReportStatus.Dismissed;
            report.AdminNote = adminNote;
            report.ResolvedAt = DateTimeOffset.UtcNow;

            if (hideReview && report.RoomingHouseReview != null)
            {
                report.RoomingHouseReview.IsHidden = true;

                await _context.SaveChangesAsync(cancellationToken);
                await RoomingHouseRatingHelper.UpdateRatingAsync(_context, report.RoomingHouseReview.RoomingHouseId, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }


}
