namespace Palloncino.Models.Enums;

public enum JobOrderStatus
{
    Pending = 1,          // Just created, not started
    InProgress = 2,       // At least one task started
    ReadyForDelivery = 3, // All preparation/design done
    OutForDelivery = 4,   // Driver on the way
    Delivered = 5,        // Delivered but rentals not returned yet
    WaitingReturn = 6,    // Waiting for rental items to be returned
    Completed = 7,        // Fully completed (rentals returned or skipped)
    Cancelled = 8,        // Cancelled
    Overdue = 9           // Past due date not completed
}