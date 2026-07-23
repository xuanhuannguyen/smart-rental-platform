using Microsoft.Extensions.DependencyInjection;
using SmartRentalPlatform.Application.AdminApproval;
using SmartRentalPlatform.Application.Administrative;
using SmartRentalPlatform.Application.Amenities;
using SmartRentalPlatform.Application.Auth;
using SmartRentalPlatform.Application.Billing;
using SmartRentalPlatform.Application.Chat;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Application.Kyc;
using SmartRentalPlatform.Application.LandlordDashboard;
using SmartRentalPlatform.Application.Notifications;
using SmartRentalPlatform.Application.Payments;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Application.RentalRequests;
using SmartRentalPlatform.Application.RoomingHouses;
using SmartRentalPlatform.Application.RoomingHouses.Search;
using SmartRentalPlatform.Application.ReviewReports;
using SmartRentalPlatform.Application.RoomDeposits;
using SmartRentalPlatform.Application.Rooms;
using SmartRentalPlatform.Application.Users;
using SmartRentalPlatform.Application.ViewingAppointments;
using SmartRentalPlatform.Application.Wallets;

namespace SmartRentalPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAdministrativeService, AdministrativeService>();
        services.AddScoped<IAmenityService, AmenityService>();
        services.AddAuthServices();
        services.AddScoped<IKycService, KycService>();
        services.AddScoped<IRentalRequestService, RentalRequestService>();
        services.AddRentalContractServices();
        services.AddScoped<IRoomDepositService, RoomDepositService>();
        services.AddRoomingHouseServices();
        services.AddRoomServices();
        services.AddScoped<IUserService, UserService>();
        services.AddAdminApprovalServices();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IWithdrawalService, WithdrawalService>();
        services.AddScoped<IWithdrawalWebhookService, WithdrawalWebhookService>();
        services.AddScoped<IPayOSTopUpService, PayOSTopUpService>();
        services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
        services.AddScoped<IMockPaymentService, MockPaymentService>();
        services.AddBillingServices();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IViewingAppointmentService, ViewingAppointmentService>();
        services.AddScoped<ILandlordDashboardService, LandlordDashboardService>();
        return services;
    }
}
