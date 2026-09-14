namespace Models;

public class CjData
{
    public List<CjContent>? Content { get; set; }

    // Paging metadata from /v1/product/listV2. TotalRecords is capped at 6000 by CJ,
    // so a single category query can never enumerate more than that.
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
}
