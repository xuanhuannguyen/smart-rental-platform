using System.Collections.Generic;

namespace SmartRentalPlatform.Contracts.Admin;

public class AdminUserListResponse
{
    public List<AdminUserListItemResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public int PageNumber { get; set; }
}
