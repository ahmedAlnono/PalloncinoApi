using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Task = System.Threading.Tasks.Task;
using Palloncino.Models.DTOs;

namespace Palloncino.Services.Implementations;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;
    private readonly FirebaseMessaging? _firebaseMessaging;

    [Obsolete]
    public NotificationService(
        ApplicationDbContext context,
        ILogger<NotificationService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        
        // Initialize Firebase if credentials exist
        var firebaseCredentialPath = configuration["Firebase:CredentialPath"];
        if (!string.IsNullOrEmpty(firebaseCredentialPath) && File.Exists(firebaseCredentialPath))
        {
            try
            {
                if (FirebaseApp.DefaultInstance == null)
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromFile(firebaseCredentialPath)
                    });
                }
                _firebaseMessaging = FirebaseMessaging.DefaultInstance;
                _logger.LogInformation("Firebase messaging initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Firebase messaging");
            }
        }
        else
        {
            _logger.LogWarning("Firebase credentials not found. Push notifications disabled.");
        }
    }
    
    // ========== Order Notifications ==========
    
    public async Task SendOrderCreatedNotification(int orderId, int customerId)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for notification", orderId);
            return;
        }
        
        var title = "Order Received! 🎉";
        var body = $"Your order has been received successfully. Order #{orderId} is now under review.";
        
        // Save to database
        await SaveNotification(customerId, title, body, NotificationType.OrderUpdate, orderId, "Order");
        
        // Send push notification
        await SendPushNotificationAsync(customerId, title, body, new Dictionary<string, string>
        {
            { "type", "order_created" },
            { "orderId", orderId.ToString() }
        });
        
        _logger.LogInformation("Order created notification sent to Customer {CustomerId} for Order {OrderId}", 
            customerId, orderId);
    }
    
    public async Task SendNewCustomOrderNotification(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for notification", orderId);
            return;
        }
        
        var title = "❗ New Custom Order Request";
        var body = $"Customer {order.Customer?.FullName} has submitted a new custom order request. Please review.";
        
        // Send to all admins and employees
        var adminUsers = await _context.Users
            .Where(u => (u.Role == UserRole.Admin || u.Role == UserRole.Employee) 
                && u.Status == UserStatus.Active 
                && !u.IsDeleted)
            .ToListAsync();
        
        foreach (var admin in adminUsers)
        {
            await SaveNotification(admin.Id, title, body, NotificationType.Alert, orderId, "Order");
            await SendPushNotificationAsync(admin.Id, title, body, new Dictionary<string, string>
            {
                { "type", "custom_order" },
                { "orderId", orderId.ToString() }
            });
        }
        
        _logger.LogInformation("New custom order notification sent to {Count} admin users for Order {OrderId}", 
            adminUsers.Count, orderId);
    }
    
    public async Task SendOrderApprovedNotification(int orderId, int customerId)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.JobOrder)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for notification", orderId);
            return;
        }
        
        var title = "✅ Order Approved!";
        var body = $"Great news! Your order #{orderId} has been approved and is now being prepared.";
        
        if (order.JobOrder != null)
        {
            body += $" Your tracking number is {order.JobOrder.JobNumber}.";
        }
        
        // Save to database
        await SaveNotification(customerId, title, body, NotificationType.OrderUpdate, orderId, "Order");
        
        // Send push notification
        await SendPushNotificationAsync(customerId, title, body, new Dictionary<string, string>
        {
            { "type", "order_approved" },
            { "orderId", orderId.ToString() },
            { "jobNumber", order.JobOrder?.JobNumber ?? "" }
        });
        
        _logger.LogInformation("Order approved notification sent to Customer {CustomerId} for Order {OrderId}", 
            customerId, orderId);
    }
    
    public async Task SendOrderRejectedNotification(int orderId, int customerId, string reason)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for notification", orderId);
            return;
        }
        
        var title = "❌ Order Update";
        var body = $"Unfortunately, your order #{orderId} could not be approved at this time.";
        
        if (!string.IsNullOrEmpty(reason))
        {
            body += $"\nReason: {reason}";
        }
        
        body += "\n\nPlease contact support if you have any questions.";
        
        // Save to database
        await SaveNotification(customerId, title, body, NotificationType.Alert, orderId, "Order");
        
        // Send push notification
        await SendPushNotificationAsync(customerId, title, body, new Dictionary<string, string>
        {
            { "type", "order_rejected" },
            { "orderId", orderId.ToString() },
            { "reason", reason }
        });
        
        _logger.LogInformation("Order rejected notification sent to Customer {CustomerId} for Order {OrderId}. Reason: {Reason}", 
            customerId, orderId, reason);
    }
    
    // ========== Job Order Notifications ==========
    
    public async Task SendJobOrderCreatedNotification(int jobOrderId, int assignedTo)
    {
        var jobOrder = await _context.JobOrders
            .Include(j => j.Branch)
            .FirstOrDefaultAsync(j => j.Id == jobOrderId);
        
        if (jobOrder == null)
        {
            _logger.LogWarning("JobOrder {JobOrderId} not found for notification", jobOrderId);
            return;
        }
        
        var title = "📋 New Job Order Assigned";
        var body = $"You have been assigned to Job Order {jobOrder.JobNumber}. Due date: {jobOrder.DueAt:yyyy-MM-dd HH:mm}";
        
        await SaveNotification(assignedTo, title, body, NotificationType.TaskAssigned, jobOrderId, "JobOrder");
        await SendPushNotificationAsync(assignedTo, title, body, new Dictionary<string, string>
        {
            { "type", "job_order_assigned" },
            { "jobOrderId", jobOrderId.ToString() },
            { "jobNumber", jobOrder.JobNumber }
        });
        
        _logger.LogInformation("JobOrder assigned notification sent to User {AssignedTo} for JobOrder {JobOrderId}", 
            assignedTo, jobOrderId);
    }
    
    public async Task SendTaskAssignedNotification(int taskId, int assigneeId)
    {
        var task = await _context.Tasks
            .Include(t => t.JobOrder)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        
        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for notification", taskId);
            return;
        }
        
        var title = "📌 New Task Assigned";
        var body = $"Task: {task.Title}\nJob Order: {task.JobOrder?.JobNumber}\nDue: {task.DueAt:yyyy-MM-dd HH:mm}";
        
        await SaveNotification(assigneeId, title, body, NotificationType.TaskAssigned, taskId, "Task");
        await SendPushNotificationAsync(assigneeId, title, body, new Dictionary<string, string>
        {
            { "type", "task_assigned" },
            { "taskId", taskId.ToString() },
            { "jobOrderId", task.JobOrderId.ToString() }
        });
        
        _logger.LogInformation("Task assigned notification sent to User {AssigneeId} for Task {TaskId}", 
            assigneeId, taskId);
    }
    
    public async Task SendTaskCompletedByOtherNotification(int taskId, int originalAssigneeId, int completedBy)
    {
        var task = await _context.Tasks
            .Include(t => t.JobOrder)
            .Include(t => t.Completer)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        
        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for notification", taskId);
            return;
        }
        
        var title = "✓ Task Completed by Colleague";
        var body = $"Task '{task.Title}' was completed by {task.Completer?.FullName} on your behalf.\nJob Order: {task.JobOrder?.JobNumber}";
        
        await SaveNotification(originalAssigneeId, title, body, NotificationType.Alert, taskId, "Task");
        await SendPushNotificationAsync(originalAssigneeId, title, body, new Dictionary<string, string>
        {
            { "type", "task_completed_by_other" },
            { "taskId", taskId.ToString() },
            { "completedBy", completedBy.ToString() }
        });
        
        _logger.LogInformation("Task completed by other notification sent to User {OriginalAssigneeId} for Task {TaskId}. Completed by {CompletedBy}", 
            originalAssigneeId, taskId, completedBy);
    }
    
    public async Task SendTaskReminderNotification(int taskId, int assigneeId)
    {
        var task = await _context.Tasks
            .Include(t => t.JobOrder)
            .FirstOrDefaultAsync(t => t.Id == taskId);
        
        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for reminder", taskId);
            return;
        }
        
        var timeRemaining = task.DueAt - DateTime.UtcNow;
        var timeString = timeRemaining.TotalHours < 1 
            ? $"{timeRemaining.Minutes} minutes" 
            : $"{timeRemaining.Hours} hours";
        
        var title = "⏰ Task Reminder";
        var body = $"Task '{task.Title}' is due in {timeString}. Please complete it soon.\nJob Order: {task.JobOrder?.JobNumber}";
        
        await SaveNotification(assigneeId, title, body, NotificationType.TaskReminder, taskId, "Task");
        await SendPushNotificationAsync(assigneeId, title, body, new Dictionary<string, string>
        {
            { "type", "task_reminder" },
            { "taskId", taskId.ToString() }
        });
        
        _logger.LogDebug("Task reminder notification sent to User {AssigneeId} for Task {TaskId}", assigneeId, taskId);
    }
    
    public async Task SendDeliveryChecklistReminderNotification(int jobOrderId, int driverId)
    {
        var jobOrder = await _context.JobOrders
            .FirstOrDefaultAsync(j => j.Id == jobOrderId);
        
        if (jobOrder == null)
        {
            _logger.LogWarning("JobOrder {JobOrderId} not found for delivery reminder", jobOrderId);
            return;
        }
        
        var title = "🚚 Delivery Checklist Reminder";
        var body = $"Don't forget to complete the delivery checklist for Job Order {jobOrder.JobNumber}.\nRemember to take photos as proof for each phase.";
        
        await SaveNotification(driverId, title, body, NotificationType.TaskReminder, jobOrderId, "JobOrder");
        await SendPushNotificationAsync(driverId, title, body, new Dictionary<string, string>
        {
            { "type", "delivery_checklist" },
            { "jobOrderId", jobOrderId.ToString() }
        });
        
        _logger.LogInformation("Delivery checklist reminder sent to Driver {DriverId} for JobOrder {JobOrderId}", 
            driverId, jobOrderId);
    }
    
    // ========== Inventory Notifications ==========
    
    public async Task SendLowStockAlertAsync(int inventoryItemId)
    {
        var item = await _context.InventoryItems
            .Include(i => i.Branch)
            .FirstOrDefaultAsync(i => i.Id == inventoryItemId);
        
        if (item == null)
        {
            _logger.LogWarning("Inventory item {ItemId} not found for low stock alert", inventoryItemId);
            return;
        }
        
        var title = "⚠️ Low Stock Alert";
        var body = $"Item '{item.Title}' (SKU: {item.Sku}) is low on stock.\nCurrent quantity: {item.Quantity} | Minimum level: {item.MinStockLevel ?? 0}";
        
        // Send to branch manager and admins
        var recipients = await _context.Users
            .Where(u => (u.Role == UserRole.Admin || u.Role == UserRole.Employee) 
                && u.Status == UserStatus.Active 
                && !u.IsDeleted
                && (u.BranchId == item.BranchId || u.Role == UserRole.Admin))
            .ToListAsync();
        
        foreach (var recipient in recipients)
        {
            await SaveNotification(recipient.Id, title, body, NotificationType.Alert, inventoryItemId, "InventoryItem");
            await SendPushNotificationAsync(recipient.Id, title, body, new Dictionary<string, string>
            {
                { "type", "low_stock" },
                { "itemId", inventoryItemId.ToString() }
            });
        }
        
        _logger.LogWarning("Low stock alert sent for item {ItemTitle} (SKU: {Sku}). Current quantity: {Quantity}", 
            item.Title, item.Sku, item.Quantity);
    }
    
    // ========== Admin Notifications ==========
    
    public async Task SendBroadcastNotificationAsync(BroadcastNotificationDto broadcast)
    {
        var query = _context.Users.Where(u => u.Status == UserStatus.Active && !u.IsDeleted);
        
        if (broadcast.TargetRoles != null && broadcast.TargetRoles.Any())
        {
            var roles = broadcast.TargetRoles.Select(r => r.ToString()).ToList();
            query = query.Where(u => roles.Contains(u.Role.ToString()));
        }
        
        if (broadcast.BranchId.HasValue)
        {
            query = query.Where(u => u.BranchId == broadcast.BranchId.Value);
        }
        
        var users = await query.ToListAsync();
        
        foreach (var user in users)
        {
            await SaveNotification(user.Id, broadcast.Title, broadcast.Body, broadcast.Type, null, null);
            await SendPushNotificationAsync(user.Id, broadcast.Title, broadcast.Body, new Dictionary<string, string>
            {
                { "type", "broadcast" },
                { "imageUrl", broadcast.ImageUrl ?? "" }
            });
        }
        
        _logger.LogInformation("Broadcast notification sent to {Count} users. Title: {Title}", 
            users.Count, broadcast.Title);
    }
    
    public async Task SendInternalNotificationAsync(int recipientId, string title, string body, NotificationType type, int? relatedEntityId = null, string? relatedEntityType = null)
    {
        await SaveNotification(recipientId, title, body, type, relatedEntityId, relatedEntityType);
        await SendPushNotificationAsync(recipientId, title, body, new Dictionary<string, string>
        {
            { "type", type.ToString().ToLower() },
            { "entityId", relatedEntityId?.ToString() ?? "" },
            { "entityType", relatedEntityType ?? "" }
        });
    }
    
    // ========== Push Notification Methods ==========
    
    public async Task SendPushNotificationAsync(int userId, string title, string body, Dictionary<string, string>? data = null)
    {
        // Save to database first (always)
        var notification = await SaveNotification(userId, title, body, NotificationType.General, null, null);
        
        // Try to send push notification via Firebase
        if (_firebaseMessaging == null)
        {
            _logger.LogDebug("Firebase not configured. Push notification not sent to User {UserId}", userId);
            return;
        }
        
        try
        {
            // Get user's FCM tokens
            var userDeviceTokens = await _context.UserDeviceTokens
                .Where(t => t.UserId == userId && t.IsActive)
                .Select(t => t.Token)
                .ToListAsync();
            
            if (!userDeviceTokens.Any())
            {
                _logger.LogDebug("No device tokens found for User {UserId}", userId);
                return;
            }
            
            var message = new MulticastMessage
            {
                Tokens = userDeviceTokens,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data ?? new Dictionary<string, string>(),
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = "palloncino_notifications",
                        Priority = NotificationPriority.HIGH,
                        ClickAction = "FLUTTER_NOTIFICATION_CLICK"
                    }
                },
                Apns = new ApnsConfig
                {
                    Headers = new Dictionary<string, string>
                    {
                        { "apns-priority", "10" }
                    },
                    Aps = new Aps
                    {
                        Sound = "default",
                        Badge = 1
                    }
                }
            };
            
            var response = await _firebaseMessaging.SendEachForMulticastAsync(message);
            
            _logger.LogInformation("Push notification sent to User {UserId}. Success: {SuccessCount}, Failure: {FailureCount}", 
                userId, response.SuccessCount, response.FailureCount);
            
            // Handle failed tokens (remove inactive ones)
            for (int i = 0; i < response.Responses.Count; i++)
            {
                var resp = response.Responses[i];
                if (!resp.IsSuccess && resp.Exception != null)
                {
                    var failedToken = userDeviceTokens[i];
                    _logger.LogWarning("Failed to send push to token {Token}: {Error}", failedToken, resp.Exception.Message);
                    
                    // Optionally mark token as inactive
                    var tokenEntity = await _context.UserDeviceTokens
                        .FirstOrDefaultAsync(t => t.Token == failedToken);
                    if (tokenEntity != null)
                    {
                        tokenEntity.IsActive = false;
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification to User {UserId}", userId);
        }
    }
    
    public async Task SendPushNotificationToRoleAsync(UserRole role, string title, string body, Dictionary<string, string>? data = null)
    {
        var users = await _context.Users
            .Where(u => u.Role == role && u.Status == UserStatus.Active && !u.IsDeleted)
            .Select(u => u.Id)
            .ToListAsync();
        
        foreach (var userId in users)
        {
            await SendPushNotificationAsync(userId, title, body, data);
        }
        
        _logger.LogInformation("Push notification sent to role {Role} ({Count} users)", role, users.Count);
    }
    
    public async Task SendPushNotificationToBranchAsync(int branchId, string title, string body, Dictionary<string, string>? data = null)
    {
        var users = await _context.Users
            .Where(u => u.BranchId == branchId && u.Status == UserStatus.Active && !u.IsDeleted)
            .Select(u => u.Id)
            .ToListAsync();
        
        foreach (var userId in users)
        {
            await SendPushNotificationAsync(userId, title, body, data);
        }
        
        _logger.LogInformation("Push notification sent to branch {BranchId} ({Count} users)", branchId, users.Count);
    }
    
    // ========== Private Helper Methods ==========
    
    private async Task<Models.Entities.Notification> SaveNotification(int recipientId, string title, string body, NotificationType type, int? relatedEntityId, string? relatedEntityType)
    {
        var notification = new Models.Entities.Notification
        {
            RecipientId = recipientId,
            Title = title,
            Body = body,
            Type = type,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            IsRead = false,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        
        return notification;
    }

    public Task SendWelcomeNotification(User user)
    {
        throw new NotImplementedException();
    }

    public Task SendLowStockNotification(int inventoryItemId, int currentStock)
    {
        throw new NotImplementedException();
    }
}