using Microsoft.Extensions.DependencyInjection;

namespace SmartRentalPlatform.Application.RentalContracts;

internal static class RentalContractServiceRegistration
{
    public static IServiceCollection AddRentalContractServices(this IServiceCollection services)
    {
        services.AddScoped<IContractPdfRenderer, ContractPdfRenderer>();
        services.AddScoped<IContractDocumentModelFactory, ContractDocumentModelFactory>();
        services.AddScoped<IContractFileService, ContractFileService>();
        services.AddScoped<IContractSignatureOtpService, ContractSignatureOtpService>();
        services.AddScoped<IContractPreviewAttachmentService, ContractPreviewAttachmentService>();
        services.AddScoped<IContractESignService, ContractESignService>();
        services.AddScoped<RentalContractPreviewBuilder>();
        services.AddScoped<RentalContractOccupantValidator>();
        services.AddScoped<RentalContractDocumentHelper>();
        services.AddScoped<RentalContractFinalInvoiceStatusResolver>();
        services.AddScoped<ContractAppendixRenderOptionsBuilder>();
        services.AddScoped<ContractAppendixQueryLoader>();
        services.AddScoped<ContractAppendixOccupantAccountResolver>();
        services.AddScoped<IContractAppendixService, ContractAppendixService>();
        services.AddScoped<IRentalContractService, RentalContractService>();

        return services;
    }
}
