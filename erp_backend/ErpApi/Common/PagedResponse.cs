namespace ErpApi.Common;

// بلا حقل Success: دائماً مغلَّف داخل ApiResponse<PagedResponse<T>> ليبقى شكل الاستجابة واحداً
public class PagedResponse<T>
{
    public List<T> Data { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => PageNumber < TotalPages;
}
