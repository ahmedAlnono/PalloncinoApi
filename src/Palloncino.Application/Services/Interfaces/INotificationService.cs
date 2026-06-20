using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Task = System.Threading.Tasks.Task;

namespace Palloncino.Services.Interfaces;

public interface INotificationService
{
    Task SendTaskAssignedNotification(int taskId, int userId);
    Task SendTaskCompletedByOtherNotification(int taskId, int assignedToId, int completedById);
    Task SendWelcomeNotification(User user);
    Task SendJobOrderCreatedNotification(int jobOrderId, int coordinatorId);
    Task SendLowStockNotification(int inventoryItemId, int currentStock);
        Task SendOrderCreatedNotification(int orderId, int customerId);
    Task SendNewCustomOrderNotification(int orderId);
    Task SendOrderApprovedNotification(int orderId, int customerId);
    Task SendOrderRejectedNotification(int orderId, int customerId, string reason);
    Task SendPaymentConfirmationNotificationAsync(int orderId, int customerId);
    
    // ========== Job Order Notifications ==========
    Task SendTaskReminderNotification(int taskId, int assigneeId);
    Task SendDeliveryChecklistReminderNotification(int jobOrderId, int driverId);
    
    // ========== Inventory Notifications ==========
    Task SendLowStockAlertAsync(int inventoryItemId);
    
    // ========== Admin Notifications ==========
    Task SendBroadcastNotificationAsync(BroadcastNotificationDto broadcast);
    Task SendInternalNotificationAsync(int recipientId, string title, string body, NotificationType type, int? relatedEntityId = null, string? relatedEntityType = null);
    
    // ========== Push Notification Methods ==========
    Task SendPushNotificationAsync(int userId, string title, string body, Dictionary<string, string>? data = null);
    Task SendPushNotificationToRoleAsync(UserRole role, string title, string body, Dictionary<string, string>? data = null);
    Task SendPushNotificationToBranchAsync(int branchId, string title, string body, Dictionary<string, string>? data = null);
}