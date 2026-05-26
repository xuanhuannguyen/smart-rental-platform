namespace SmartRentalPlatform.Contracts.Common;

public class PagedResult<T>
{
    public List<T> Items { get ; set ;} = new ();
    public int Page {get ; set ;} // trang hien tai
    public int PageSize { get ; set ;} // item moi trang
    public int TotalItems { get ; set ;} // tong item
    public int TotalPages { get ; set ;} // tong trang
}
