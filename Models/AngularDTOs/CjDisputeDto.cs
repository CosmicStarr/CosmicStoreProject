namespace Models.AngularDTOs;

public class CjDisputeDto
{
    public string? DisputeId { get; set; }
    public string? Status { get; set; }
    public string? DisputeReason { get; set; }
    public decimal Money { get; set; }
    public int? FinallyDeal { get; set; }
    public string? FinallyDealLabel { get; set; }
    public bool ReturnReceived { get; set; }
    public string? CreateDate { get; set; }
}

public class CjDisputeProduct
{
    public string? LineItemId { get; set; }
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public bool CanChoose { get; set; }
}

public class CjDisputeReason
{
    public int DisputeReasonId { get; set; }
    public string? ReasonName { get; set; }
}

public class CjDisputeConfirmInfo
{
    public IList<CjDisputeProduct> Products { get; set; } = new List<CjDisputeProduct>();
    public IList<CjDisputeReason> Reasons { get; set; } = new List<CjDisputeReason>();
}

public class CjCreateDisputeRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string BusinessDisputeId { get; set; } = string.Empty;
    public int DisputeReasonId { get; set; }
    public int ExpectType { get; set; } = 1;
    public int RefundType { get; set; } = 1;
    public string MessageText { get; set; } = string.Empty;
    public IList<CjDisputeProduct> Products { get; set; } = new List<CjDisputeProduct>();
}

public class OpenReturnDisputeRequest
{
    public string ReturnTrackingNumber { get; set; } = string.Empty;
    public string? Message { get; set; }
}
