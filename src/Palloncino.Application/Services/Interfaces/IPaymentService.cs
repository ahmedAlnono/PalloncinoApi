using Palloncino.Models.DTOs;

namespace Palloncino.Services.Interfaces;

public interface IPaymentService
{
    Task<CheckoutSessionResponseDto> CreateCheckoutSessionAsync(int orderId, int customerId, string? successUrl = null, string? cancelUrl = null);
    Task<PaymentIntentResponseDto> CreatePaymentIntentAsync(int orderId, int customerId);
    Task<OrderPaymentStatusDto> GetOrderPaymentStatusAsync(int orderId, int? requestingUserId = null, bool isAdmin = false);
    Task HandleWebhookAsync(string json, string stripeSignature);
}
