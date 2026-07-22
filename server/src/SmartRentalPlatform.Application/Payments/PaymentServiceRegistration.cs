using Microsoft.Extensions.DependencyInjection;

namespace SmartRentalPlatform.Application.Payments;

internal static class PaymentServiceRegistration
{
    public static IServiceCollection AddPaymentServices(this IServiceCollection services)
    {
        services.AddScoped<IPayOSTopUpService, PayOSTopUpService>();
        services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
        services.AddScoped<IMockPaymentService, MockPaymentService>();

        return services;
    }
}
