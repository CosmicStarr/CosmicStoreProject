using System.Text.Json.Serialization;

namespace Models;

/// <summary>
/// Response shape of /v1/product/getCategory. Unlike most CJ endpoints, "data" is an
/// array rather than an object, so it cannot reuse <see cref="CjResponse"/>.
/// </summary>
public class CjCategoryResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public List<CjCategoryFirst>? Data { get; set; }
}

public class CjCategoryFirst
{
    public string? CategoryFirstName { get; set; }
    public List<CjCategorySecond>? CategoryFirstList { get; set; }
}

public class CjCategorySecond
{
    public string? CategorySecondName { get; set; }
    public List<CjCategoryThird>? CategorySecondList { get; set; }
}

public class CjCategoryThird
{
    /// <summary>Third-level category id, the only level listV2 accepts as a filter.</summary>
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
}
