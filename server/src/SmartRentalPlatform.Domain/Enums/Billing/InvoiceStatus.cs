namespace SmartRentalPlatform.Domain.Enums.Billing;

public enum InvoiceStatus
{
    Draft,
    Issued,
    PartiallyPaid,
    Paid,
    Overdue,
    Cancelled
}
