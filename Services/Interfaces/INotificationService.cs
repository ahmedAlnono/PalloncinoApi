using Palloncino.Models.Entities;

namespace Palloncino.Services.Interfaces;

public interface INotificationService
{
    System.Threading.Tasks.Task SendTaskAssignedNotification(int taskId, int userId);
    System.Threading.Tasks.Task SendTaskCompletedByOtherNotification(int taskId, int assignedToId, int completedById);
    System.Threading.Tasks.Task SendWelcomeNotification(User user);
    System.Threading.Tasks.Task SendJobOrderCreatedNotification(int jobOrderId, int coordinatorId);
    System.Threading.Tasks.Task SendLowStockNotification(int inventoryItemId, int currentStock);
}