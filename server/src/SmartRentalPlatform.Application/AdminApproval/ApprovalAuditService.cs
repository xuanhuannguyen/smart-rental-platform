using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.AdminApproval;

namespace SmartRentalPlatform.Application.AdminApproval;

public class ApprovalAuditService : IApprovalAuditService
{
    private readonly IAppDbContext _context;

    public ApprovalAuditService(IAppDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
        Guid adminId,
        string approvalType,
        Guid entityId,
        string action,
        string? reason = null,
        string? additionalInfo = null,
        CancellationToken cancellationToken = default)
    {
        _context.ApprovalAuditLogs.Add(new ApprovalAuditLog
        {
            Id = Guid.NewGuid(),
            AdminId = adminId,
            ApprovalType = approvalType,
            EntityId = entityId,
            Action = action,
            Reason = reason,
            AdditionalInfo = additionalInfo,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}
