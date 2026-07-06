using Microsoft.Extensions.DependencyInjection;

namespace SmartRentalPlatform.Application.AdminApproval;

internal static class AdminApprovalServiceRegistration
{
    public static IServiceCollection AddAdminApprovalServices(this IServiceCollection services)
    {
        services.AddScoped<IAdminKycApprovalService, AdminKycApprovalService>();
        services.AddScoped<IAdminRoomingHouseApprovalService, AdminRoomingHouseApprovalService>();
        services.AddScoped<IApprovalAuditService, ApprovalAuditService>();
        services.AddScoped<IAdminUserService, AdminUserService>();

        return services;
    }
}
