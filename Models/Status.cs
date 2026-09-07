using System.Runtime.Serialization;

namespace Models;

public enum Status
{
    [EnumMember(Value= "Pending")]
    Pending,
    [EnumMember(Value= "Payment Recevied")]
    PaymentRecevied,
    [EnumMember(Value= "Payment Failed")]
    PaymentFailed,
    [EnumMember(Value= "Payment Cancelled")]  
    PaymentCancelled,
    [EnumMember(Value= "Payment On Hold")]
    PaymentOnHold,
    [EnumMember(Value = "Processing")]
    Processing,
    [EnumMember(Value = "Submitted")]
    Submitted,
    [EnumMember(Value = "Paid")]
    Paid
}
