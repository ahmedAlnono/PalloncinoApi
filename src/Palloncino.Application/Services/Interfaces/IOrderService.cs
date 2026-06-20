using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;

namespace Palloncino.Services.Interfaces;

public interface IOrderService
{
    // ========== Customer Endpoints ==========
    Task<Order> CreateOrderAsync(Order order, List<OrderItem> items, List<Attachment>? attachments = null);
    Task<Order> CreateCustomOrderAsync(Order order, string description, List<Attachment>? attachments = null);
    Task<IEnumerable<Order>> GetCustomerOrdersAsync(int customerId);
    Task<Order?> GetOrderByIdAsync(int orderId);
    
    // ========== Admin/Employee Endpoints ==========
    Task<IEnumerable<Order>> GetAllOrdersAsync(OrderStatus? status = null, int? customerId = null);
    Task<Order> ApproveOrderAsync(int orderId, int approvedBy);
    Task<Order> RejectOrderAsync(int orderId, int rejectedBy, string reason);
    Task<Order> UpdateOrderStatusAsync(int orderId, OrderStatus status, int updatedBy, string? reason = null);
    
    // ========== Validation ==========
    Task<bool> OrderExistsAsync(int orderId);
    Task<bool> CanApproveOrderAsync(int orderId);
    Task<bool> CanRejectOrderAsync(int orderId);
    
    // ========== Statistics ==========
    Task<OrderStatisticsDto> GetOrderStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null);
}