using Microsoft.Extensions.DependencyInjection;
using SmartRentalPlatform.Application.AdminApproval;
using SmartRentalPlatform.Application.Administrative;
using SmartRentalPlatform.Application.Amenities;
using SmartRentalPlatform.Application.Auth;
using SmartRentalPlatform.Application.Billing;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Kyc;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Application.RentalRequests;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Application.RoomingHouses.Search;
using SmartRentalPlatform.Application.RoomDeposits;
using SmartRentalPlatform.Application.Rooms;
using SmartRentalPlatform.Application.Users;
using SmartRentalPlatform.Application.ViewingAppointments;

namespace SmartRentalPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAdministrativeService, AdministrativeService>();
        services.AddScoped<IAmenityService, AmenityService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuthSessionService, AuthSessionService>();
        services.AddScoped<IAuthPasswordService, AuthPasswordService>();
        services.AddScoped<IGoogleLoginService, GoogleLoginService>();
        services.AddScoped<IKycService, KycService>();
        services.AddScoped<IRentalRequestService, RentalRequestService>();
        services.AddScoped<IContractPdfRenderer, ContractPdfRenderer>();
        services.AddScoped<IContractFileService, ContractFileService>();
        services.AddScoped<IContractSignatureOtpService, ContractSignatureOtpService>();
        services.AddScoped<IContractAppendixService, ContractAppendixService>();
        services.AddScoped<IRentalContractService, RentalContractService>();
        services.AddScoped<IRoomDepositService, RoomDepositService>();
        services.AddScoped<IRoomingHouseQueryService, RoomingHouseQueryService>();
        services.AddScoped<IRoomingHouseSearchParser, RoomingHouseSearchParser>();
        services.AddScoped<IRoomingHouseRuleService, RoomingHouseRuleService>();
        services.AddScoped<IRoomingHouseRentalPolicyService, RoomingHouseRentalPolicyService>();
        services.AddScoped<IRoomingHouseDraftService, RoomingHouseDraftService>();
        services.AddScoped<IRoomingHouseMediaService, RoomingHouseMediaService>();
        services.AddScoped<IRoomingHouseSubmissionService, RoomingHouseSubmissionService>();
        services.AddScoped<RoomAccessService>();
        services.AddScoped<IRoomQueryService, RoomQueryService>();
        services.AddScoped<IRoomCommandService, RoomCommandService>();
        services.AddScoped<IRoomMediaService, RoomMediaService>();
        services.AddScoped<IRoomPriceTierService, RoomPriceTierService>();
        services.AddScoped<IRoomStatusService, RoomStatusService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminKycApprovalService, AdminKycApprovalService>();
        services.AddScoped<IAdminRoomingHouseApprovalService, AdminRoomingHouseApprovalService>();
        services.AddScoped<IApprovalAuditService, ApprovalAuditService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IBillingContractReadService, ContractBillingReadService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IViewingAppointmentService, ViewingAppointmentService>();
        return services;
    }
}
