namespace Palloncino.Models.Enums;

public enum OrderStatus
{
    PendingReview = 1,    // Waiting for admin approval
    Approved = 2,         // Approved, ready to convert to Job Order
    Rejected = 3,         // Rejected with reason
    Converted = 4,        // Converted to Job Order
    Cancelled = 5         // Cancelled by customer or admin
}