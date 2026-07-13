using Microsoft.Extensions.DependencyInjection;
using SmartRentalPlatform.Application.ReviewReports;

namespace SmartRentalPlatform.Application.AdminApproval;

internal static class AdminApprovalServiceRegistration
{
    public static IServiceCollection AddAdminApprovalServices(this IServiceCollection services)
    {
        services.AddScoped<IAdminKycApprovalService, AdminKycApprovalService>();
        services.AddScoped<IAdminRoomingHouseApprovalService, AdminRoomingHouseApprovalService>();
        services.AddScoped<IApprovalAuditService, ApprovalAuditService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IReviewReportService, ReviewReportService>();
        services.AddScoped<IReviewModerationAdminService, ReviewModerationAdminService>();

        return services;
    }
}
