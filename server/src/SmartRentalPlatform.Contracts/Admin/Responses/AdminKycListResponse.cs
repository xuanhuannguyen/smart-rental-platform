using System.Collections.Generic;

namespace SmartRentalPlatform.Contracts.Admin;

public class AdminKycListResponse
{
    public List<AdminKycListItemResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public int PageNumber { get; set; }
}
