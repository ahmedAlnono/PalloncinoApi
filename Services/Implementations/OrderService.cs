using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;

namespace Palloncino.Services.Implementations;

public class OrderService(
    ApplicationDbContext context,
    ILogger<OrderService> logger,
    IJobOrderService jobOrderService,
    INotificationService notificationService) : IOrderService
{
    // ========== Customer Endpoints ==========
    
    public async Task<Order> CreateOrderAsync(Order order, List<OrderItem> items, List<Attachment>? attachments = null)
    {
        // Validate order items
        if (items == null || !items.Any())
            throw new InvalidOperationException("Order must contain at least one item");
        
        // Calculate total amount
        decimal totalAmount = 0;
        foreach (var item in items)
        {
            if (item.UnitPrice <= 0)
                throw new InvalidOperationException($"Invalid price for item: {item.ItemName}");
            
            if (item.Quantity <= 0)
                throw new InvalidOperationException($"Invalid quantity for item: {item.ItemName}");
            
            item.TotalPrice = item.Quantity * item.UnitPrice;
            totalAmount += item.TotalPrice;
        }
        
        // Add delivery fee if applicable
        if (order.DeliveryFee.HasValue)
            totalAmount += order.DeliveryFee.Value;
        
        order.TotalAmount = totalAmount;
        order.Status = OrderStatus.PendingReview;
        order.Source = OrderSource.MobileApp;
        order.CreatedAt = DateTime.UtcNow;
        order.IsActive = true;
        
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        
        // Add order items
        foreach (var item in items)
        {
            item.OrderId = order.Id;
            context.OrderItems.Add(item);
        }
        
        // Add attachments if provided
        if (attachments != null && attachments.Any())
        {
            foreach (var attachment in attachments)
            {
                attachment.EntityId = order.Id;
                attachment.Type = AttachmentType.Order;
                attachment.CreatedAt = DateTime.UtcNow;
                context.Attachments.Add(attachment);
            }
        }
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Order created: OrderId {OrderId} by Customer {CustomerId}, Total: {TotalAmount}", 
            order.Id, order.CustomerId, order.TotalAmount);
        
        // Send notification to customer
        await notificationService.SendOrderCreatedNotification(order.Id, order.CustomerId);
        
        return order;
    }
    
    public async Task<Order> CreateCustomOrderAsync(Order order, string description, List<Attachment>? attachments = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidOperationException("Custom order description is required");
        
        order.Type = OrderType.Custom;
        order.CustomDesignDescription = description;
        order.Status = OrderStatus.PendingReview;
        order.Source = OrderSource.MobileApp;
        order.TotalAmount = 0; // Will be set after quotation
        order.CreatedAt = DateTime.UtcNow;
        order.IsActive = true;
        
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        
        // Add attachments
        if (attachments != null && attachments.Any())
        {
            foreach (var attachment in attachments)
            {
                attachment.EntityId = order.Id;
                attachment.Type = AttachmentType.Order;
                attachment.CreatedAt = DateTime.UtcNow;
                context.Attachments.Add(attachment);
            }
            await context.SaveChangesAsync();
        }
        
        logger.LogInformation("Custom order created: OrderId {OrderId} by Customer {CustomerId}", 
            order.Id, order.CustomerId);
        
        // Send notification to admin
        await notificationService.SendNewCustomOrderNotification(order.Id);
        
        return order;
    }
    
    public async Task<IEnumerable<Order>> GetCustomerOrdersAsync(int customerId)
    {
        return await context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.Attachments)
            .Include(o => o.JobOrder)
            .Where(o => o.CustomerId == customerId && !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<Order?> GetOrderByIdAsync(int orderId)
    {
        return await context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems!)
                .ThenInclude(oi => oi.CatalogItem)
            .Include(o => o.Attachments)
            .Include(o => o.JobOrder)
            .Include(o => o.Quotations)
            .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
    }
    
    // ========== Admin/Employee Endpoints ==========
    
    public async Task<IEnumerable<Order>> GetAllOrdersAsync(OrderStatus? status = null, int? customerId = null)
    {
        var query = context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .Include(o => o.JobOrder)
            .Where(o => !o.IsDeleted);
        
        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);
        
        if (customerId.HasValue)
            query = query.Where(o => o.CustomerId == customerId.Value);
        
        return await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<Order> ApproveOrderAsync(int orderId, int approvedBy)
    {
        var order = await GetOrderByIdAsync(orderId);
        if (order == null)
            throw new InvalidOperationException($"Order with ID {orderId} not found");
        
        if (order.Status != OrderStatus.PendingReview)
            throw new InvalidOperationException($"Order cannot be approved. Current status: {order.Status}");
        
        order.Status = OrderStatus.Approved;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = approvedBy;
        
        await context.SaveChangesAsync();
        
        // Create Job Order automatically (FR-ORD-03)
        var jobOrder = new JobOrder
        {
            SourceOrderId = order.Id,
            BranchId = order.Customer?.BranchId ?? 1, // Default branch
            ExecutionType = ExecutionType.DeliveryOnly,
            DueAt = order.RequiredDate ?? DateTime.UtcNow.AddDays(3),
            Status = JobOrderStatus.Pending,
            SpecialInstructions = order.Notes,
            DeliveryAddress = order.Address,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = approvedBy
        };
        
        var createdJobOrder = await jobOrderService.CreateJobOrderAsync(jobOrder);
        
        // Update order with job order reference
        order.JobOrder = createdJobOrder;
        order.Status = OrderStatus.Converted;
        await context.SaveChangesAsync();
        
        logger.LogInformation("Order {OrderId} approved by User {ApprovedBy}. JobOrder {JobNumber} created", 
            orderId, approvedBy, createdJobOrder.JobNumber);
        
        // Send notification to customer
        await notificationService.SendOrderApprovedNotification(orderId, order.CustomerId);
        
        return order;
    }
    
    public async Task<Order> RejectOrderAsync(int orderId, int rejectedBy, string reason)
    {
        var order = await GetOrderByIdAsync(orderId);
        if (order == null)
            throw new InvalidOperationException($"Order with ID {orderId} not found");
        
        if (order.Status != OrderStatus.PendingReview)
            throw new InvalidOperationException($"Order cannot be rejected. Current status: {order.Status}");
        
        order.Status = OrderStatus.Rejected;
        order.RejectionReason = reason;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = rejectedBy;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Order {OrderId} rejected by User {RejectedBy}. Reason: {Reason}", 
            orderId, rejectedBy, reason);
        
        // Send notification to customer with rejection reason (FR-ORD-04)
        await notificationService.SendOrderRejectedNotification(orderId, order.CustomerId, reason);
        
        return order;
    }
    
    public async Task<Order> UpdateOrderStatusAsync(int orderId, OrderStatus status, int updatedBy, string? reason = null)
    {
        var order = await GetOrderByIdAsync(orderId);
        if (order == null)
            throw new InvalidOperationException($"Order with ID {orderId} not found");
        
        if (!IsValidStatusTransition(order.Status, status))
            throw new InvalidOperationException($"Invalid status transition from {order.Status} to {status}");
        
        var oldStatus = order.Status;
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = updatedBy;
        
        if (status == OrderStatus.Rejected && !string.IsNullOrEmpty(reason))
            order.RejectionReason = reason;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Order {OrderId} status changed from {OldStatus} to {NewStatus} by User {UpdatedBy}", 
            orderId, oldStatus, status, updatedBy);
        
        // Send notification based on status
        if (status == OrderStatus.Approved)
            await notificationService.SendOrderApprovedNotification(orderId, order.CustomerId);
        else if (status == OrderStatus.Rejected && reason != null)
            await notificationService.SendOrderRejectedNotification(orderId, order.CustomerId, reason);
        
        return order;
    }
    
    // ========== Validation ==========
    
    public async Task<bool> OrderExistsAsync(int orderId)
    {
        return await context.Orders
            .AnyAsync(o => o.Id == orderId && !o.IsDeleted);
    }
    
    public async Task<bool> CanApproveOrderAsync(int orderId)
    {
        var order = await GetOrderByIdAsync(orderId);
        return order != null && order.Status == OrderStatus.PendingReview;
    }
    
    public async Task<bool> CanRejectOrderAsync(int orderId)
    {
        var order = await GetOrderByIdAsync(orderId);
        return order != null && order.Status == OrderStatus.PendingReview;
    }
    
    // ========== Statistics ==========
    
    public async Task<OrderStatisticsDto> GetOrderStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = context.Orders.Where(o => !o.IsDeleted);
        
        if (fromDate.HasValue)
            query = query.Where(o => o.CreatedAt >= fromDate.Value);
        
        if (toDate.HasValue)
            query = query.Where(o => o.CreatedAt <= toDate.Value);
        
        var orders = await query.ToListAsync();
        
        return new OrderStatisticsDto
        {
            TotalOrders = orders.Count,
            PendingOrders = orders.Count(o => o.Status == OrderStatus.PendingReview),
            ApprovedOrders = orders.Count(o => o.Status == OrderStatus.Approved),
            RejectedOrders = orders.Count(o => o.Status == OrderStatus.Rejected),
            ConvertedOrders = orders.Count(o => o.Status == OrderStatus.Converted),
            TotalRevenue = orders.Where(o => o.Status == OrderStatus.Converted).Sum(o => o.TotalAmount),
            AverageOrderValue = orders.Any(o => o.Status == OrderStatus.Converted) 
                ? orders.Where(o => o.Status == OrderStatus.Converted).Average(o => o.TotalAmount) 
                : 0
        };
    }
    
    // ========== Private Helper Methods ==========
    
    private bool IsValidStatusTransition(OrderStatus from, OrderStatus to)
    {
        return (from, to) switch
        {
            (OrderStatus.PendingReview, OrderStatus.Approved) => true,
            (OrderStatus.PendingReview, OrderStatus.Rejected) => true,
            (OrderStatus.Approved, OrderStatus.Converted) => true,
            (OrderStatus.Approved, OrderStatus.Cancelled) => true,
            (_, OrderStatus.Cancelled) when from != OrderStatus.Converted => true,
            _ => false
        };
    }
}
