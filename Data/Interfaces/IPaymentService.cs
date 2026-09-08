using Models;
using Models.AngularDTOs;

namespace Data.Interfaces;

public interface IPaymentService
{
    /// <summary>
    /// Creates or updates the Stripe PaymentIntent for a cart. The charge amount is always
    /// recomputed from current database prices plus the selected shipping, never trusted from the client.
    /// </summary>
    Task<PaymentIntentDto?> CreateOrUpdatePaymentAsync(string cartId, string? userId, PaymentIntentRequest request);

    /// <summary>Reads a PaymentIntent straight from Stripe so the server can verify it before fulfilling.</summary>
    Task<Stripe.PaymentIntent?> GetPaymentIntentAsync(string paymentIntentId);

    Task<Order?> MarkPaymentSucceededAsync(string paymentIntentId);

    Task<Order?> MarkPaymentFailedAsync(string paymentIntentId);
}
