namespace SmartRentalPlatform.Application.AdminApproval;

public interface IApprovalAuditService
{
    Task LogAsync(
        Guid adminId,
        string approvalType,
        Guid entityId,
        string action,
        string? reason = null,
        string? additionalInfo = null,
        CancellationToken cancellationToken = default);
}
