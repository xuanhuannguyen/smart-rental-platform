using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Infrastructure.BackgroundServices;

public class ESignEnvelopeExpirationWorker : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<ESignEnvelopeExpirationWorker> logger;

    public ESignEnvelopeExpirationWorker(IServiceProvider serviceProvider, ILogger<ESignEnvelopeExpirationWorker> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                
                var now = DateTimeOffset.UtcNow;
                
                var expiredEnvelopes = await dbContext.ContractSigningEnvelopes
                    .Where(e => e.Status == SigningEnvelopeStatus.WaitingForSigners || e.Status == SigningEnvelopeStatus.PartiallySigned)
                    .Where(e => e.ExpiresAt.HasValue && e.ExpiresAt.Value <= now)
                    .ToListAsync(stoppingToken);

                foreach (var envelope in expiredEnvelopes)
                {
                    envelope.Status = SigningEnvelopeStatus.Expired;
                    
                    var signatures = await dbContext.ContractSignatures
                        .Where(s => s.ContractSigningEnvelopeId == envelope.Id && (s.Status == ContractSignatureStatus.Pending || s.Status == ContractSignatureStatus.Notified || s.Status == ContractSignatureStatus.Viewed))
                        .ToListAsync(stoppingToken);
                        
                    foreach(var sig in signatures)
                    {
                        sig.Status = ContractSignatureStatus.Expired;
                    }
                    
                    if (envelope.RentalContractId.HasValue)
                    {
                        var contract = await dbContext.RentalContracts.FindAsync(new object[] { envelope.RentalContractId.Value }, stoppingToken);
                        if (contract != null && contract.Status == RentalContractStatus.PendingTenantSignature)
                        {
                            contract.Status = RentalContractStatus.Expired;
                        }
                    }
                }

                if (expiredEnvelopes.Any())
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                    logger.SafeLogInformation("Expired {Count} eSign envelopes", expiredEnvelopes.Count);
                }
            }
            catch (Exception ex)
            {
                logger.SafeLogError(ex, "Error in ESignEnvelopeExpirationWorker");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
