using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;

public class Payment : BaseEntity
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "usd";
    public PaymentStatus Status { get; set; }
    public string? StripeCheckoutSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? StripeCustomerId { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? FailureReason { get; set; }
    public string? IdempotencyKey { get; set; }

    public virtual Order? Order { get; set; }
}
