namespace Palloncino.Models.Enums;

public enum QuotationStatus
{
    Draft = 1,        // Being prepared
    Sent = 2,         // Sent to customer
    Approved = 3,     // Customer approved
    Expired = 4,      // ValidUntil date passed
    Rejected = 5,     // Customer rejected
    Converted = 6     // Converted to order
}