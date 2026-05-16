namespace Palloncino.Models.Configuration;

public class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string Currency { get; set; } = "usd";
    public string SuccessUrl { get; set; } = "https://palloncino.com/orders/payment/success?session_id={CHECKOUT_SESSION_ID}";
    public string CancelUrl { get; set; } = "https://palloncino.com/orders/payment/cancel";
}
