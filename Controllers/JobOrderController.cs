using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;

namespace Palloncino.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JobOrderController(
    IJobOrderService jobOrderService,
    IOrderService orderService,
    IMapper mapper,
    ILogger<JobOrderController> logger
) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<JobOrderDto>> CreateJobOrder([FromBody] CreateJobOrderDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            JobOrder jobOrder;

            if (dto.SourceOrderId.HasValue)
            {
                // Create from an existing order
                var order = await orderService.GetOrderByIdAsync(dto.SourceOrderId.Value);
                if (order == null)
                    return BadRequest(new { message = "Source order not found" });

                if (order.Status != OrderStatus.Approved)
                {
                    // Optionally approve it if it's not already
                    await orderService.ApproveOrderAsync(order.Id, userId);
                }

                jobOrder = new JobOrder
                {
                    SourceOrderId = dto.SourceOrderId,
                    ExecutionType = dto.ExecutionType,
                    DueAt = dto.DueAt,
                    BranchId = dto.BranchId,
                    AssignedToCoordinator = dto.AssignedToCoordinator,
                    SpecialInstructions = dto.SpecialInstructions,
                    DeliveryAddress = dto.DeliveryAddress,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    Status = JobOrderStatus.Pending,
                    IsActive = true
                };
                
                // Note: The service might handle copying items from the source order.
                // If not, we would need to map OrderItems to JobOrderItems here.
                var createdJobOrder = await jobOrderService.CreateJobOrderAsync(jobOrder);
                return CreatedAtAction(nameof(GetJobOrder), new { id = createdJobOrder.Id }, mapper.Map<JobOrderDto>(createdJobOrder));
            }
            else
            {
                // Manual creation
                jobOrder = new JobOrder
                {
                    ExecutionType = dto.ExecutionType,
                    DueAt = dto.DueAt,
                    BranchId = dto.BranchId,
                    AssignedToCoordinator = dto.AssignedToCoordinator,
                    SpecialInstructions = dto.SpecialInstructions,
                    DeliveryAddress = dto.DeliveryAddress,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    Status = JobOrderStatus.Pending,
                    IsActive = true
                };

                var createdJobOrder = await jobOrderService.CreateJobOrderAsync(jobOrder);
                return CreatedAtAction(nameof(GetJobOrder), new { id = createdJobOrder.Id }, mapper.Map<JobOrderDto>(createdJobOrder));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating job order");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<IEnumerable<JobOrderListDto>>> GetJobOrders([FromQuery] JobOrderFilter filter)
    {
        try
        {
            var jobOrders = await jobOrderService.GetAllJobOrdersAsync(filter);
            
            // Sort by proximity to delivery date (DueAt)
            var sortedJobOrders = jobOrders.OrderBy(jo => jo.DueAt);
            
            return Ok(mapper.Map<IEnumerable<JobOrderListDto>>(sortedJobOrders));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting job orders");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<JobOrderDto>> GetJobOrder(int id)
    {
        try
        {
            var jobOrder = await jobOrderService.GetJobOrderByIdAsync(id);
            if (jobOrder == null)
                return NotFound();

            return Ok(mapper.Map<JobOrderDto>(jobOrder));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting job order {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<JobOrderDto>> UpdateJobOrder(int id, [FromBody] UpdateJobOrderDto dto)
    {
        try
        {
            var existingJobOrder = await jobOrderService.GetJobOrderByIdAsync(id);
            if (existingJobOrder == null)
                return NotFound();

            var userId = GetCurrentUserId();
            
            // Update fields if provided
            if (dto.ExecutionType.HasValue) existingJobOrder.ExecutionType = dto.ExecutionType.Value;
            if (dto.DueAt.HasValue) existingJobOrder.DueAt = dto.DueAt.Value;
            if (dto.AssignedToCoordinator.HasValue) existingJobOrder.AssignedToCoordinator = dto.AssignedToCoordinator.Value;
            if (dto.SpecialInstructions != null) existingJobOrder.SpecialInstructions = dto.SpecialInstructions;
            if (dto.DeliveryAddress != null) existingJobOrder.DeliveryAddress = dto.DeliveryAddress;
            if (dto.Status.HasValue) existingJobOrder.Status = dto.Status.Value;

            existingJobOrder.UpdatedBy = userId;
            existingJobOrder.UpdatedAt = DateTime.UtcNow;

            var updatedJobOrder = await jobOrderService.UpdateJobOrderAsync(existingJobOrder);
            return Ok(mapper.Map<JobOrderDto>(updatedJobOrder));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating job order {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/countdown")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult> GetJobOrderCountdown(int id)
    {
        try
        {
            var jobOrder = await jobOrderService.GetJobOrderByIdAsync(id);
            if (jobOrder == null)
                return NotFound();

            var timeRemaining = jobOrder.DueAt - DateTime.UtcNow;
            
            return Ok(new
            {
                jobOrderId = id,
                dueAt = jobOrder.DueAt,
                remainingSeconds = timeRemaining.TotalSeconds,
                display = FormatCountdown(timeRemaining.TotalSeconds)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting countdown for job order {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<JobOrderDto>> UpdateJobOrderStatus(int id, [FromBody] UpdateJobOrderStatusDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var updatedJobOrder = await jobOrderService.UpdateJobOrderStatusAsync(id, dto.Status, userId, dto.Reason);
            return Ok(mapper.Map<JobOrderDto>(updatedJobOrder));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating status for job order {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    private int GetCurrentUserId()
    {
        string? id = (User.Claims.FirstOrDefault(u => u.Type == "userId")?.Value)
            ?? (User.Claims.FirstOrDefault(u => u.Type == "sub")?.Value)
            ?? throw new Exception("User ID not found in token");
        return int.Parse(id);
    }

    private static string FormatCountdown(double totalSeconds)
    {
        if (totalSeconds <= 0)
            return "Overdue";
        
        var timeSpan = TimeSpan.FromSeconds(totalSeconds);
        
        if (timeSpan.TotalHours >= 24)
            return $"{(int)timeSpan.TotalDays}d {(int)timeSpan.Hours}h";
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours}h {(int)timeSpan.Minutes}m";
        if (timeSpan.TotalMinutes >= 1)
            return $"{(int)timeSpan.Minutes}m {(int)timeSpan.Seconds}s";
        
        return $"{(int)timeSpan.Seconds}s";
    }
}