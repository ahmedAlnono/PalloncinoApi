using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Implementations;
using Palloncino.Services.Interfaces;

namespace Palloncino.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController(
    IOrderService orderService,
    IUserService userService,
    IMapper mapper,
    IFileStorageService fileStorageService) : ControllerBase
{

    // ========== Customer Endpoints ==========

    /// <summary>
    /// POST /api/orders - إنشاء طلب عادي من الكتالوج
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto createDto)
    {
        var customerId = GetCurrentUserId();
        var order = mapper.Map<Order>(createDto);
        order.CustomerId = customerId;
        order.Type = OrderType.Regular;
        
        var items = mapper.Map<List<OrderItem>>(createDto.Items);
        
        try
        {
            var created = await orderService.CreateOrderAsync(order, items);
            var orderDto = mapper.Map<OrderDto>(created);
            
            return Ok(new
            {
                success = true,
                message = "Order created successfully",
                data = orderDto
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    /// <summary>
    /// POST /api/orders/custom - إنشاء طلب خاص مع صور ووصف
    /// </summary>
    [HttpPost("custom")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> CreateCustomOrder([FromForm] CreateCustomOrderDto createDto)
    {
        var customerId = GetCurrentUserId();
        
        var order = new Order
        {
            CustomerId = customerId,
            Notes = createDto.Notes,
            RequiredDate = createDto.RequiredDate,
            Address = createDto.Address,
            Type = OrderType.Custom
        };
        
        var attachments = new List<Attachment>();
        
        // Upload attachments if provided
        if (createDto.Attachments != null && createDto.Attachments.Any())
        {
            foreach (var file in createDto.Attachments)
            {
                var fileUrl = await fileStorageService.UploadFileAsync(file, "orders/custom");
                attachments.Add(new Attachment
                {
                    FileName = file.FileName,
                    FileUrl = fileUrl,
                    FileType = file.ContentType,
                    FileSize = file.Length,
                    UploadedBy = customerId,
                    Type = AttachmentType.Order,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        
        try
        {
            var created = await orderService.CreateCustomOrderAsync(order, createDto.Description, attachments);
            var orderDto = mapper.Map<OrderDto>(created);
            
            return Ok(new
            {
                success = true,
                message = "Custom order created successfully. Admin will review and provide quotation.",
                data = orderDto
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    /// <summary>
    /// GET /api/orders/my - طلبات العميل الحالي
    /// </summary>
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyOrders()
    {
        var userId = GetCurrentUserId();
        var user = await userService.GetUserByIdAsync(userId);
        
        if (user?.Role == UserRole.Customer)
        {
            var orders = await orderService.GetCustomerOrdersAsync(userId);
            var orderDtos = mapper.Map<IEnumerable<OrderDto>>(orders);
            return Ok(new { success = true, data = orderDtos });
        }
        
        // For internal staff, show all orders they can access
        var allOrders = await orderService.GetAllOrdersAsync();
        var allOrderDtos = mapper.Map<IEnumerable<OrderDto>>(allOrders);
        return Ok(new { success = true, data = allOrderDtos });
    }
    
    /// <summary>
    /// GET /api/orders/:id - تفاصيل طلب
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetOrderById(int id)
    {
        var order = await orderService.GetOrderByIdAsync(id);
        if (order == null)
            return NotFound(new { success = false, message = "Order not found" });
        
        // Check authorization
        var userId = GetCurrentUserId();
        var user = await userService.GetUserByIdAsync(userId);
        
        if (user?.Role == UserRole.Customer && order.CustomerId != userId)
            return Forbid();
        
        var orderDto = mapper.Map<OrderDto>(order);
        
        return Ok(new { success = true, data = orderDto });
    }
    
    // ========== Admin/Employee Endpoints ==========
    
    /// <summary>
    /// GET /api/orders - قائمة الطلبات (Admin/Employee)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetAllOrders([FromQuery] OrderStatus? status, [FromQuery] int? customerId)
    {
        var orders = await orderService.GetAllOrdersAsync(status, customerId);
        var orderDtos = mapper.Map<IEnumerable<OrderDto>>(orders);
        
        return Ok(new
        {
            success = true,
            count = orderDtos.Count(),
            data = orderDtos
        });
    }
    
    /// <summary>
    /// PUT /api/orders/:id/approve - قبول الطلب → يُنشئ Job Order تلقائياً
    /// </summary>
    [HttpPut("{id}/approve")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ApproveOrder(int id)
    {
        var canApprove = await orderService.CanApproveOrderAsync(id);
        if (!canApprove)
            return BadRequest(new { success = false, message = "Order cannot be approved" });
        
        try
        {
            var approved = await orderService.ApproveOrderAsync(id, GetCurrentUserId());
            var orderDto = mapper.Map<OrderDto>(approved);
            
            return Ok(new
            {
                success = true,
                message = "Order approved successfully. Job Order has been created.",
                data = orderDto
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    /// <summary>
    /// PUT /api/orders/:id/reject - رفض الطلب مع سبب → يُشعر العميل
    /// </summary>
    [HttpPut("{id}/reject")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> RejectOrder(int id, [FromBody] RejectOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { success = false, message = "Rejection reason is required" });
        
        var canReject = await orderService.CanRejectOrderAsync(id);
        if (!canReject)
            return BadRequest(new { success = false, message = "Order cannot be rejected" });
        
        try
        {
            var rejected = await orderService.RejectOrderAsync(id, GetCurrentUserId(), request.Reason);
            var orderDto = mapper.Map<OrderDto>(rejected);
            
            return Ok(new
            {
                success = true,
                message = "Order rejected successfully. Customer has been notified.",
                data = orderDto
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    /// <summary>
    /// PUT /api/orders/:id/status - تحديث حالة الطلب
    /// </summary>
    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        try
        {
            var updated = await orderService.UpdateOrderStatusAsync(
                id, 
                request.Status, 
                GetCurrentUserId(), 
                request.Reason);
            
            var orderDto = mapper.Map<OrderDto>(updated);
            
            return Ok(new
            {
                success = true,
                message = $"Order status updated to {request.Status}",
                data = orderDto
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    /// <summary>
    /// GET /api/orders/statistics - إحصائيات الطلبات (Admin)
    /// </summary>
    [HttpGet("statistics")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetOrderStatistics([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var statistics = await orderService.GetOrderStatisticsAsync(fromDate, toDate);
        return Ok(new { success = true, data = statistics });
    }
    
    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirst("userId")?.Value ?? "0");
    }
}

// ========== Request DTOs ==========

public class CreateCustomOrderDto
{
    public string Description { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Address { get; set; }
    public List<IFormFile>? Attachments { get; set; }
}

public class RejectOrderRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
    public string? Reason { get; set; }
}