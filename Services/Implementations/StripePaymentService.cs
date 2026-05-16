using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Palloncino.Data;
using Palloncino.Models.Configuration;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;
using Stripe;
using Stripe.Checkout;
using Task = System.Threading.Tasks.Task;

namespace Palloncino.Services.Implementations;

public class StripePaymentService(
    ApplicationDbContext context,
    IOptions<StripeOptions> stripeOptions,
    INotificationService notificationService,
    ILogger<StripePaymentService> logger) : IPaymentService
{
    private readonly StripeOptions _stripe = stripeOptions.Value;

    public async Task<CheckoutSessionResponseDto> CreateCheckoutSessionAsync(
        int orderId, int customerId, string? successUrl = null, string? cancelUrl = null)
    {
        EnsureStripeConfigured();

        var order = await GetPayableOrderAsync(orderId, customerId);
        var stripeCustomerId = await GetOrCreateStripeCustomerAsync(order.Customer!);

        var payment = await CreatePendingPaymentAsync(order, stripeCustomerId, "checkout");

        var lineItems = BuildLineItems(order);
        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Customer = stripeCustomerId,
            Mode = "payment",
            LineItems = lineItems,
            SuccessUrl = successUrl ?? _stripe.SuccessUrl,
            CancelUrl = cancelUrl ?? _stripe.CancelUrl,
            Metadata = new Dictionary<string, string>
            {
                { "orderId", order.Id.ToString() },
                { "paymentId", payment.Id.ToString() }
            }
        });

        payment.StripeCheckoutSessionId = session.Id;
        order.PaymentStatus = PaymentStatus.Pending;
        await context.SaveChangesAsync();

        logger.LogInformation("Checkout session {SessionId} created for Order {OrderId}", session.Id, orderId);

        return new CheckoutSessionResponseDto
        {
            SessionId = session.Id,
            CheckoutUrl = session.Url ?? string.Empty,
            PaymentId = payment.Id
        };
    }

    public async Task<PaymentIntentResponseDto> CreatePaymentIntentAsync(int orderId, int customerId)
    {
        EnsureStripeConfigured();

        var order = await GetPayableOrderAsync(orderId, customerId);
        var stripeCustomerId = await GetOrCreateStripeCustomerAsync(order.Customer!);
        var payment = await CreatePendingPaymentAsync(order, stripeCustomerId, "intent");

        var amountInCents = ToStripeAmount(order.TotalAmount);
        var intentService = new PaymentIntentService();
        var intent = await intentService.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = amountInCents,
            Currency = _stripe.Currency,
            Customer = stripeCustomerId,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
            Metadata = new Dictionary<string, string>
            {
                { "orderId", order.Id.ToString() },
                { "paymentId", payment.Id.ToString() }
            }
        });

        payment.StripePaymentIntentId = intent.Id;
        order.PaymentStatus = PaymentStatus.Pending;
        await context.SaveChangesAsync();

        logger.LogInformation("Payment intent {IntentId} created for Order {OrderId}", intent.Id, orderId);

        return new PaymentIntentResponseDto
        {
            PaymentIntentId = intent.Id,
            ClientSecret = intent.ClientSecret ?? string.Empty,
            PaymentId = payment.Id,
            Amount = order.TotalAmount,
            Currency = _stripe.Currency
        };
    }

    public async Task<OrderPaymentStatusDto> GetOrderPaymentStatusAsync(
        int orderId, int? requestingUserId = null, bool isAdmin = false)
    {
        var order = await context.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);

        if (order == null)
            throw new InvalidOperationException($"Order with ID {orderId} not found");

        if (!isAdmin && requestingUserId.HasValue && order.CustomerId != requestingUserId.Value)
            throw new UnauthorizedAccessException("You do not have access to this order's payment status");

        var latestPayment = order.Payments?
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault();

        return new OrderPaymentStatusDto
        {
            OrderId = order.Id,
            PaymentStatus = order.PaymentStatus,
            TotalAmount = order.TotalAmount,
            RequiresPayment = order.TotalAmount > 0 && order.PaymentStatus != PaymentStatus.Paid,
            LatestPayment = latestPayment == null ? null : MapPayment(latestPayment)
        };
    }

    public async Task HandleWebhookAsync(string json, string stripeSignature)
    {
        if (string.IsNullOrWhiteSpace(_stripe.WebhookSecret))
        {
            logger.LogWarning("Stripe webhook secret is not configured. Skipping webhook processing.");
            return;
        }

        var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, _stripe.WebhookSecret);

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                var session = stripeEvent.Data.Object as Session;
                if (session != null)
                    await MarkPaymentSucceededAsync(session.Id, session.PaymentIntentId, isCheckoutSession: true);
                break;

            case EventTypes.PaymentIntentSucceeded:
                var intent = stripeEvent.Data.Object as PaymentIntent;
                if (intent != null)
                    await MarkPaymentSucceededAsync(intent.Id, intent.Id, isCheckoutSession: false);
                break;

            case EventTypes.PaymentIntentPaymentFailed:
                var failedIntent = stripeEvent.Data.Object as PaymentIntent;
                if (failedIntent != null)
                    await MarkPaymentFailedAsync(failedIntent.Id, failedIntent.LastPaymentError?.Message);
                break;

            case EventTypes.CheckoutSessionExpired:
                var expiredSession = stripeEvent.Data.Object as Session;
                if (expiredSession != null)
                    await MarkPaymentExpiredAsync(expiredSession.Id);
                break;

            default:
                logger.LogDebug("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                break;
        }
    }

    private async Task MarkPaymentSucceededAsync(string sessionOrIntentId, string? paymentIntentId, bool isCheckoutSession)
    {
        var payment = await FindPaymentAsync(sessionOrIntentId, isCheckoutSession);
        if (payment == null)
        {
            logger.LogWarning("Payment not found for Stripe id {Id}", sessionOrIntentId);
            return;
        }

        if (payment.Status == PaymentStatus.Paid)
            return;

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(paymentIntentId))
            payment.StripePaymentIntentId = paymentIntentId;

        var order = await context.Orders.FindAsync(payment.OrderId);
        if (order != null)
        {
            order.PaymentStatus = PaymentStatus.Paid;
            await notificationService.SendPaymentConfirmationNotificationAsync(order.Id, order.CustomerId);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Payment {PaymentId} marked as paid for Order {OrderId}", payment.Id, payment.OrderId);
    }

    private async Task MarkPaymentFailedAsync(string paymentIntentId, string? reason)
    {
        var payment = await context.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);

        if (payment == null)
            return;

        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = reason;

        var order = await context.Orders.FindAsync(payment.OrderId);
        if (order != null && order.PaymentStatus != PaymentStatus.Paid)
            order.PaymentStatus = PaymentStatus.Failed;

        await context.SaveChangesAsync();
        logger.LogWarning("Payment {PaymentId} failed for Order {OrderId}: {Reason}", payment.Id, payment.OrderId, reason);
    }

    private async Task MarkPaymentExpiredAsync(string sessionId)
    {
        var payment = await context.Payments
            .FirstOrDefaultAsync(p => p.StripeCheckoutSessionId == sessionId);

        if (payment == null || payment.Status == PaymentStatus.Paid)
            return;

        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = "Checkout session expired";

        var order = await context.Orders.FindAsync(payment.OrderId);
        if (order != null && order.PaymentStatus == PaymentStatus.Pending)
            order.PaymentStatus = PaymentStatus.Unpaid;

        await context.SaveChangesAsync();
    }

    private async Task<Payment?> FindPaymentAsync(string id, bool isCheckoutSession)
    {
        if (isCheckoutSession)
        {
            return await context.Payments
                .FirstOrDefaultAsync(p => p.StripeCheckoutSessionId == id);
        }

        return await context.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == id);
    }

    private async Task<Order> GetPayableOrderAsync(int orderId, int customerId)
    {
        var order = await context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);

        if (order == null)
            throw new InvalidOperationException($"Order with ID {orderId} not found");

        if (order.CustomerId != customerId)
            throw new UnauthorizedAccessException("You do not have permission to pay for this order");

        if (order.TotalAmount <= 0)
            throw new InvalidOperationException("This order does not require payment");

        if (order.PaymentStatus == PaymentStatus.Paid)
            throw new InvalidOperationException("This order has already been paid");

        if (order.Status is OrderStatus.Rejected or OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot pay for order with status {order.Status}");

        return order;
    }

    private async Task<Payment> CreatePendingPaymentAsync(Order order, string stripeCustomerId, string idempotencySuffix)
    {
        var existingPending = await context.Payments
            .Where(p => p.OrderId == order.Id && p.Status == PaymentStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        if (existingPending != null)
            return existingPending;

        var payment = new Payment
        {
            OrderId = order.Id,
            Amount = order.TotalAmount,
            Currency = _stripe.Currency,
            Status = PaymentStatus.Pending,
            StripeCustomerId = stripeCustomerId,
            IdempotencyKey = $"order-{order.Id}-{idempotencySuffix}-{DateTime.UtcNow:yyyyMMddHHmmss}"
        };

        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        return payment;
    }

    private async Task<string> GetOrCreateStripeCustomerAsync(User user)
    {
        if (!string.IsNullOrEmpty(user.StripeCustomerId))
            return user.StripeCustomerId;

        var customerService = new CustomerService();
        var customer = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = user.Email,
            Name = user.FullName,
            Phone = user.Phone,
            Metadata = new Dictionary<string, string> { { "userId", user.Id.ToString() } }
        });

        user.StripeCustomerId = customer.Id;
        await context.SaveChangesAsync();
        return customer.Id;
    }

    private List<SessionLineItemOptions> BuildLineItems(Order order)
    {
        var items = new List<SessionLineItemOptions>();

        if (order.OrderItems != null && order.OrderItems.Any())
        {
            foreach (var item in order.OrderItems)
            {
                items.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = _stripe.Currency,
                        UnitAmount = ToStripeAmount(item.UnitPrice),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.ItemName
                        }
                    },
                    Quantity = item.Quantity
                });
            }
        }
        else
        {
            items.Add(new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = _stripe.Currency,
                    UnitAmount = ToStripeAmount(order.TotalAmount),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = $"Order #{order.Id}"
                    }
                },
                Quantity = 1
            });
        }

        if (order.DeliveryFee is > 0)
        {
            items.Add(new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = _stripe.Currency,
                    UnitAmount = ToStripeAmount(order.DeliveryFee.Value),
                    ProductData = new SessionLineItemPriceDataProductDataOptions { Name = "Delivery Fee" }
                },
                Quantity = 1
            });
        }

        return items;
    }

    private static long ToStripeAmount(decimal amount) =>
        (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);

    private void EnsureStripeConfigured()
    {
        if (string.IsNullOrWhiteSpace(_stripe.SecretKey))
            throw new InvalidOperationException("Stripe is not configured. Set Stripe:SecretKey in configuration.");
    }

    private static PaymentDto MapPayment(Payment payment) => new()
    {
        Id = payment.Id,
        OrderId = payment.OrderId,
        Amount = payment.Amount,
        Currency = payment.Currency,
        Status = payment.Status,
        PaidAt = payment.PaidAt,
        FailureReason = payment.FailureReason,
        CreatedAt = payment.CreatedAt
    };
}
