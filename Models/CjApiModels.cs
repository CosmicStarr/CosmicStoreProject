using System.Text.Json.Serialization;

namespace Models;

/// <summary>Payload for CJ's freightCalculate endpoint.</summary>
public class CjFreightRequest
{
    public string startCountryCode { get; set; } = "CN";
    public string countryCode { get; set; } = string.Empty;
    public string? province { get; set; }
    public string? city { get; set; }
    public List<CjFreightProduct> products { get; set; } = new();
}

public class CjFreightProduct
{
    public string vid { get; set; } = string.Empty;
    public int quantity { get; set; }
}

public class CjFreightResponse
{
    public bool result { get; set; }
    public string? message { get; set; }
    public List<CjFreightOption>? data { get; set; }
}

public class CjFreightOption
{
    public string? logisticName { get; set; }
    public string? logisticAging { get; set; }
    public string? logisticPrice { get; set; }
    public string? logisticPriceCn { get; set; }
}

public class CjBalanceResponse
{
    public bool result { get; set; }
    public string? message { get; set; }
    public CjBalanceData? data { get; set; }
}

public class CjBalanceData
{
    public decimal amount { get; set; }
    public string? currency { get; set; }
}

public class CjOrderQueryResponse
{
    public bool result { get; set; }
    public string? message { get; set; }
    public CjOrderQueryData? data { get; set; }
}

public class CjOrderQueryData
{
    public string? orderId { get; set; }
    public string? orderStatus { get; set; }
    public string? trackNumber { get; set; }
    public string? logisticName { get; set; }
}

public class CjVariantQueryResponse
{
    public bool result { get; set; }
    public string? message { get; set; }
    public List<CjVariantData>? data { get; set; }
}

public class CjVariantData
{
    public string? vid { get; set; }
    public string? variantSku { get; set; }
    public string? variantNameEn { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal variantSellPrice { get; set; }

    public string? variantImage { get; set; }
}

public class CjStockResponse
{
    public bool result { get; set; }
    public List<CjStockData>? data { get; set; }
}

public class CjStockData
{
    public string? vid { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int storageNum { get; set; }
}
