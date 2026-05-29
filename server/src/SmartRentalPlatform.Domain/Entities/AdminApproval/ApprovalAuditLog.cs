namespace SmartRentalPlatform.Domain.Entities.AdminApproval;

public class ApprovalAuditLog
{
    public Guid Id { get; set; }
    public Guid AdminId { get; set; }
    public string ApprovalType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? AdditionalInfo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
