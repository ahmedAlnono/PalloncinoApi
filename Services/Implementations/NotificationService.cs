using Palloncino.Models.Entities;
using Palloncino.Services.Interfaces;

namespace Palloncino.Services.Implementations;

public class NotificationService(ILogger<NotificationService> logger) : INotificationService
{
    public System.Threading.Tasks.Task SendTaskAssignedNotification(int taskId, int userId)
    {
        logger.LogInformation("Notification: Task {TaskId} assigned to user {UserId}", taskId, userId);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task SendTaskCompletedByOtherNotification(int taskId, int assignedToId, int completedById)
    {
        logger.LogInformation("Notification: Task {TaskId} assigned to {AssignedToId} was completed by {CompletedById}", 
            taskId, assignedToId, completedById);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task SendWelcomeNotification(User user)
    {
        logger.LogInformation("Notification: Welcome email sent to {UserEmail}", user.Email);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task SendJobOrderCreatedNotification(int jobOrderId, int coordinatorId)
    {
        logger.LogInformation("Notification: JobOrder {JobOrderId} assigned to coordinator {CoordinatorId}", jobOrderId, coordinatorId);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task SendLowStockNotification(int inventoryItemId, int currentStock)
    {
        logger.LogWarning("Notification: Inventory item {InventoryItemId} is low on stock ({CurrentStock})", inventoryItemId, currentStock);
        return System.Threading.Tasks.Task.CompletedTask;
    }
}