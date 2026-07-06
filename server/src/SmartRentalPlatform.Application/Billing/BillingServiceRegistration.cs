using Microsoft.Extensions.DependencyInjection;

namespace SmartRentalPlatform.Application.Billing;

internal static class BillingServiceRegistration
{
    public static IServiceCollection AddBillingServices(this IServiceCollection services)
    {
        services.AddScoped<IBillingContractReadService, ContractBillingReadService>();
        services.AddScoped<IInvoiceWalletPaymentService, InvoiceWalletPaymentService>();
        services.AddScoped<BillingPeriodResolver>();
        services.AddScoped<BillingInvoiceBuilder>();
        services.AddScoped<InvoiceQueryLoader>();
        services.AddScoped<BillingContractContextResolver>();
        services.AddScoped<MeterReadingInputResolver>();
        services.AddScoped<BillingWorkflowGuard>();
        services.AddScoped<IBillingService, BillingService>();

        return services;
    }
}
