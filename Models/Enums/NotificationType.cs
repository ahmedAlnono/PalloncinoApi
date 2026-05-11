namespace Palloncino.Models.Enums;

public enum NotificationType
{
    OrderUpdate = 1,     // Order status changed
    TaskAssigned = 2,    // New task assigned
    TaskReminder = 3,    // Upcoming task deadline
    Alert = 4,           // Important alert (skip, rejection, etc.)
    Promotional = 5,     // Marketing/promotional messages
    General = 6,         // General announcements
    PaymentConfirmation = 7  // Payment received
}