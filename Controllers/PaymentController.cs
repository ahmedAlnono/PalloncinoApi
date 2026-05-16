using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palloncino.Models.DTOs;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;
using Stripe;

namespace Palloncino.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentController(IPaymentService paymentService) : ControllerBase
{
    /// <summary>
    /// POST /api/payments/checkout-session - Create Stripe Checkout session (web)
    /// </summary>
    [HttpPost("checkout-session")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionDto request)
    {
        try
        {
            var result = await paymentService.CreateCheckoutSessionAsync(
                request.OrderId,
                GetCurrentUserId(),
                request.SuccessUrl,
                request.CancelUrl);

            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/payments/payment-intent - Create Stripe PaymentIntent (mobile)
    /// </summary>
    [HttpPost("payment-intent")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreateCheckoutSessionDto request)
    {
        try
        {
            var result = await paymentService.CreatePaymentIntentAsync(request.OrderId, GetCurrentUserId());
            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/payments/order/{orderId} - Payment status for an order
    /// </summary>
    [HttpGet("order/{orderId}")]
    [Authorize]
    public async Task<IActionResult> GetOrderPaymentStatus(int orderId)
    {
        try
        {
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("Employee");
            var result = await paymentService.GetOrderPaymentStatusAsync(
                orderId,
                GetCurrentUserId(),
                isAdmin);

            return Ok(new { success = true, data = result });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/payments/webhook - Stripe webhook (no auth)
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();

        if (string.IsNullOrEmpty(signature))
            return BadRequest(new { success = false, message = "Missing Stripe-Signature header" });

        try
        {
            await paymentService.HandleWebhookAsync(json, signature);
            return Ok();
        }
        catch (StripeException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/payments/config - Publishable key for client SDKs
    /// </summary>
    [HttpGet("config")]
    [Authorize]
    public IActionResult GetStripeConfig([FromServices] Microsoft.Extensions.Options.IOptions<Palloncino.Models.Configuration.StripeOptions> options)
    {
        return Ok(new
        {
            success = true,
            data = new { publishableKey = options.Value.PublishableKey }
        });
    }

    private int GetCurrentUserId() =>
        int.Parse(User.FindFirst("userId")?.Value ?? "0");
}
