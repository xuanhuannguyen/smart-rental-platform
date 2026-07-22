using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartRentalPlatform.Contracts.Common;
using SmartRentalPlatform.Contracts.ReviewReports.Requests;
using SmartRentalPlatform.Contracts.ReviewReports.Responses;

namespace SmartRentalPlatform.Application.ReviewReports;

public interface IReviewReportService
{
    Task CreateReportAsync(
        Guid reviewId,
        Guid landlordUserId,
        CreateReviewReportRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ReviewReportResponse>> GetReportsAsync(
        int page,
        int pageSize,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<ReviewReportResponse> GetReportDetailAsync(
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task ResolveReportAsync(
        Guid reportId,
        Guid adminUserId,
        bool hideReview,
        string? adminNote,
        CancellationToken cancellationToken = default);
}
