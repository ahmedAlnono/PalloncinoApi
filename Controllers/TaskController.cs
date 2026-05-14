using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;
using TaskStatus = Palloncino.Models.Enums.TaskStatus;
namespace Palloncino.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class TaskController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IJobOrderService _jobOrderService;
    private readonly IInventoryService _inventoryService;
    private readonly IUserService _userService;
    private readonly ILogger<TaskController> _logger;

    public TaskController(
        ITaskService taskService,
        IJobOrderService jobOrderService,
        IInventoryService inventoryService,
        IUserService userService,
        ILogger<TaskController> logger)
    {
        _taskService = taskService;
        _jobOrderService = jobOrderService;
        _inventoryService = inventoryService;
        _userService = userService;
        _logger = logger;
    }

    // ========== Task Queries ==========

    /// <summary>
    /// GET /api/tasks - مهام المستخدم الحالي
    /// </summary>
    [HttpGet("tasks")]
    public async Task<IActionResult> GetMyTasks([FromQuery] TaskStatus? status)
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        IEnumerable<Models.Entities.Task> tasks;

        if (userRole == UserRole.Admin)
        {
            // Admin sees all tasks
            tasks = await _taskService.GetAllTasksAsync(new TaskFilter { Status = status });
        }
        else
        {
            // Regular users see only their assigned tasks
            tasks = await _taskService.GetTasksByAssigneeAsync(userId, status);
        }

        var taskDtos = tasks.Select(t => new TaskResponseDto
        {
            Id = t.Id,
            Title = t.Title?? "",
            Description = t.Description,
            Type = t.Type,
            Status = t.Status,
            DueAt = t.DueAt,
            JobOrderId = t.JobOrderId,
            JobNumber = t.JobOrder?.JobNumber,
            AssignedTo = t.AssignedTo,
            AssignedToName = t.Assignee?.FullName,
            CompletedBy = t.CompletedBy,
            CompletedByName = t.Completer?.FullName,
            CompletedAt = t.CompletedAt,
            IsOverdue = t.DueAt < DateTime.UtcNow && t.Status != TaskStatus.Completed && t.Status != TaskStatus.Skipped,
            SubTasksCount = t.SubTasks?.Count ?? 0,
            CompletedSubTasksCount = t.SubTasks?.Count(st => st.IsCompleted) ?? 0
        });

        return Ok(new { success = true, data = taskDtos });
    }

    /// <summary>
    /// GET /api/job-orders/:id/tasks - كل مهام JO معين
    /// </summary>
    [HttpGet("job-orders/{jobOrderId}/tasks")]
    public async Task<IActionResult> GetTasksByJobOrder(int jobOrderId)
    {
        // Check authorization
        var canAccess = await CanAccessJobOrder(jobOrderId);
        if (!canAccess)
            return Forbid();

        var tasks = await _taskService.GetTasksByJobOrderAsync(jobOrderId);

        var taskDtos = tasks.Select(t => new TaskDetailResponseDto
        {
            Id = t.Id,
            Title = t.Title?? "",
            Description = t.Description,
            Type = t.Type,
            Status = t.Status,
            DueAt = t.DueAt,
            AssignedTo = t.AssignedTo,
            AssignedToName = t.Assignee?.FullName,
            SkipReason = t.SkipReason,
            CompletedAt = t.CompletedAt,
            CompletedBy = t.CompletedBy,
            CompletedByName = t.Completer?.FullName,
            SubTasks = t.SubTasks?.Select(st => new SubTaskResponseDto
            {
                Id = st.Id,
                Title = st.Title ?? "",
                Description = st.Description,
                IsCompleted = st.IsCompleted,
                CompletedAt = st.CompletedAt,
                CompletedBy = st.CompletedBy,
                CompletedByName = st.Completer?.FullName,
                Order = st.Order
            }).ToList(),
            ChecklistItems = t.ChecklistItems?.Select(ci => new ChecklistItemResponseDto
            {
                Id = ci.Id,
                ItemName = ci.ItemName?? "",
                Phase = ci.Phase,
                IsChecked = ci.IsChecked,
                CheckedAt = ci.CheckedAt,
                CheckedByName = ci.Checker?.FullName,
                ProofImageUrl = ci.ProofImageUrl
            }).ToList()
        });

        return Ok(new { success = true, data = taskDtos });
    }

    // ========== Task Management ==========

    /// <summary>
    /// POST /api/job-orders/:id/tasks - إضافة مهمة يدوية
    /// </summary>
    [HttpPost("job-orders/{jobOrderId}/tasks")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> AddManualTask(int jobOrderId, [FromBody] CreateManualTaskRequest request)
    {
        var jobOrder = await _jobOrderService.GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            return NotFound(new { success = false, message = "Job order not found" });

        var task = new Models.Entities.Task
        {
            JobOrderId = jobOrderId,
            Type = request.Type,
            Title = request.Title,
            Description = request.Description,
            AssignedTo = request.AssignedTo,
            DueAt = request.DueAt ?? jobOrder.DueAt,
            Status = TaskStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId()
        };

        var subTasks = request.SubTasks?.Select(st => new CreateSubTaskDto
        {
            Title = st.Title,
            Description = st.Description,
            Order = st.Order
        }).ToList();

        try
        {
            var created = await _taskService.CreateTaskAsync(task, subTasks);
            
            return Ok(new
            {
                success = true,
                message = "Task added successfully",
                data = new { created.Id, created.Title }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/tasks/:id - تعديل مهمة
    /// </summary>
    [HttpPut("tasks/{taskId}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> UpdateTask(int taskId, [FromBody] UpdateTaskRequest request)
    {
        var existingTask = await _taskService.GetTaskByIdAsync(taskId);
        if (existingTask == null)
            return NotFound(new { success = false, message = "Task not found" });

        if (request.Title != null)
            existingTask.Title = request.Title;
        if (request.Description != null)
            existingTask.Description = request.Description;
        if (request.DueAt.HasValue)
            existingTask.DueAt = request.DueAt.Value;
        
        existingTask.UpdatedBy = GetCurrentUserId();

        try
        {
            var updated = await _taskService.UpdateTaskAsync(existingTask);
            
            return Ok(new
            {
                success = true,
                message = "Task updated successfully",
                data = new { updated.Id, updated.Title, updated.DueAt }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/tasks/:id/assign - إعادة إسناد المهمة
    /// </summary>
    [HttpPut("tasks/{taskId}/assign")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> AssignTask(int taskId, [FromBody] AssignTaskRequest request)
    {
        var task = await _taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        try
        {
            var updated = await _taskService.AssignTaskAsync(taskId, request.AssignedTo, GetCurrentUserId());
            
            return Ok(new
            {
                success = true,
                message = $"Task assigned to user {request.AssignedTo}",
                data = new { updated.Id, updated.AssignedTo }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/tasks/:id/complete - إتمام المهمة (أي مستخدم مخول → يُسجل في Activity Log)
    /// </summary>
    [HttpPut("tasks/{taskId}/complete")]
    public async Task<IActionResult> CompleteTask(int taskId)
    {
        var task = await _taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        var userId = GetCurrentUserId();
        var canComplete = await _taskService.CanCompleteTaskAsync(taskId, userId);

        if (!canComplete)
            return Forbid();

        try
        {
            Models.Entities.Task updated;
            
            if (task.AssignedTo != userId)
            {
                // Task completed by someone else - BR-12 requires logging
                await _taskService.CompleteTaskForOthersAsync(taskId, userId, task.AssignedTo ?? 0);
                updated = await _taskService.GetTaskByIdAsync(taskId);
                
                _logger.LogWarning("Task {TaskId} was completed by User {CompleterId} instead of assigned user {AssigneeId}", 
                    taskId, userId, task.AssignedTo);
            }
            else
            {
                updated = await _taskService.CompleteTaskAsync(taskId, userId);
            }
            
            return Ok(new
            {
                success = true,
                message = "Task completed successfully",
                data = new { updated.Id, updated.Status, completedBy = userId, completedAt = updated.CompletedAt }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/tasks/:id/start - بدء المهمة
    /// </summary>
    [HttpPut("tasks/{taskId}/start")]
    public async Task<IActionResult> StartTask(int taskId)
    {
        var task = await _taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        var userId = GetCurrentUserId();
        
        if (task.AssignedTo != userId && GetCurrentUserRole() != UserRole.Admin)
            return Forbid();

        try
        {
            var updated = await _taskService.StartTaskAsync(taskId, userId);
            
            return Ok(new
            {
                success = true,
                message = "Task started",
                data = new { updated.Id, updated.Status }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/tasks/:id/skip - تخطي المهمة (مع سبب)
    /// </summary>
    [HttpPut("tasks/{taskId}/skip")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> SkipTask(int taskId, [FromBody] SkipTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { success = false, message = "Skip reason is required" });

        var task = await _taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        try
        {
            var updated = await _taskService.SkipTaskAsync(taskId, GetCurrentUserId(), request.Reason);
            
            // Log skip reason in activity log (BR-08)
            _logger.LogInformation("Task {TaskId} skipped by User {UserId}. Reason: {Reason}", 
                taskId, GetCurrentUserId(), request.Reason);
            
            return Ok(new
            {
                success = true,
                message = "Task skipped",
                data = new { updated.Id, updated.Status, updated.SkipReason }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // ========== Sub-Task Management ==========

    /// <summary>
    /// POST /api/tasks/:id/subtasks - إضافة Sub-Task
    /// </summary>
    [HttpPost("tasks/{taskId}/subtasks")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> AddSubTask(int taskId, [FromBody] AddSubTaskRequest request)
    {
        var task = await _taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        try
        {
            var subTask = await _taskService.AddSubTaskAsync(taskId, request.Title, request.Description, request.Order);
            
            return Ok(new
            {
                success = true,
                message = "Sub-task added successfully",
                data = new { subTask.Id, subTask.Title, subTask.Order }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/tasks/:id/subtasks/:sid - تحديث حالة Sub-Task (done/undone)
    /// </summary>
    [HttpPut("tasks/{taskId}/subtasks/{subTaskId}")]
    public async Task<IActionResult> UpdateSubTaskStatus(int taskId, int subTaskId, [FromBody] UpdateSubTaskStatusRequest request)
    {
        var task = await _taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        // Check if user can modify this task
        var userId = GetCurrentUserId();
        if (task.AssignedTo != userId && GetCurrentUserRole() != UserRole.Admin && GetCurrentUserRole() != UserRole.Employee)
            return Forbid();

        try
        {
            SubTask updated;
            
            if (request.IsCompleted)
            {
                updated = await _taskService.CompleteSubTaskAsync(subTaskId, userId);
            }
            else
            {
                // Update title/description if provided
                updated = await _taskService.UpdateSubTaskAsync(subTaskId, request.Title, request.Description);
            }
            
            return Ok(new
            {
                success = true,
                message = request.IsCompleted ? "Sub-task completed" : "Sub-task updated",
                data = new { updated.Id, updated.Title, updated.IsCompleted }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// DELETE /api/tasks/:id/subtasks/:sid - حذف Sub-Task
    /// </summary>
    [HttpDelete("tasks/{taskId}/subtasks/{subTaskId}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> DeleteSubTask(int taskId, int subTaskId)
    {
        var task = await _taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        try
        {
            var deleted = await _taskService.RemoveSubTaskAsync(subTaskId);
            
            if (!deleted)
                return NotFound(new { success = false, message = "Sub-task not found" });
            
            return Ok(new { success = true, message = "Sub-task deleted successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // ========== Inventory Integration ==========

    /// <summary>
    /// POST /api/tasks/:id/inventory - إضافة عنصر من المخزن أثناء التنفيذ
    /// </summary>
    [HttpPost("tasks/{taskId}/inventory")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> AddInventoryItemToTask(int taskId, [FromBody] AddInventoryToTaskRequest request)
    {
        var task = await _taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        // Check if inventory item exists and has sufficient stock
        var hasStock = await _inventoryService.HasSufficientStockAsync(request.InventoryItemId, request.Quantity);
        if (!hasStock)
            return BadRequest(new { success = false, message = "Insufficient stock" });

        var inventoryItem = await _inventoryService.GetInventoryItemByIdAsync(request.InventoryItemId);
        if (inventoryItem == null)
            return NotFound(new { success = false, message = "Inventory item not found" });

        try
        {
            // Add item to job order (FR-TASK-08)
            var jobOrderItem = await _jobOrderService.AddJobOrderItemAsync(
                task.JobOrderId,
                request.InventoryItemId,
                request.Quantity,
                request.SellingPricePerUnit);

            // Add to checklist if needed
            if (request.AddToChecklist)
            {
                var checklistItem = await _taskService.AddChecklistItemAsync(
                    taskId,
                    ChecklistPhase.LoadingFromBranch,
                    $"{inventoryItem.Title} (x{request.Quantity})");
            }

            // Mark item as prepared automatically
            await _jobOrderService.MarkItemAsPreparedAsync(jobOrderItem.Id, GetCurrentUserId());

            _logger.LogInformation("Inventory item {ItemId} added to Task {TaskId} by User {UserId}. Quantity: {Quantity}", 
                request.InventoryItemId, taskId, GetCurrentUserId(), request.Quantity);

            return Ok(new
            {
                success = true,
                message = "Inventory item added to task successfully",
                data = new
                {
                    jobOrderItem.Id,
                    itemName = inventoryItem.Title,
                    jobOrderItem.QuantityUsed,
                    jobOrderItem.TotalCost
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // ========== Checklist Management ==========

    /// <summary>
    /// PUT /api/tasks/:id/checklist - تحديث حالة Checklist
    /// </summary>
    [HttpPut("tasks/{taskId}/checklist")]
    [Authorize(Roles = "Admin,Employee,Driver")]
    public async Task<IActionResult> UpdateChecklistItem(int taskId, [FromBody] UpdateChecklistRequest request)
    {
        var task = await _taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        try
        {
            var updated = await _taskService.UpdateChecklistItemAsync(
                request.ChecklistItemId,
                request.IsChecked,
                GetCurrentUserId(),
                request.ProofImageUrl);

            return Ok(new
            {
                success = true,
                message = request.IsChecked ? "Checklist item completed" : "Checklist item unchecked",
                data = new { updated.Id, updated.IsChecked, updated.CheckedAt }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// PUT /api/tasks/:id/checklist/phase - إكمال مرحلة كاملة من الـ Checklist
    /// </summary>
    [HttpPut("tasks/{taskId}/checklist/phase")]
    [Authorize(Roles = "Admin,Employee,Driver")]
    public async Task<IActionResult> CompleteChecklistPhase(int taskId, [FromBody] CompletePhaseRequest request)
    {
        var task = await _taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        try
        {
            var completed = await _taskService.CompleteChecklistPhaseAsync(taskId, request.Phase, GetCurrentUserId());
            
            return Ok(new
            {
                success = true,
                message = $"Checklist phase '{request.Phase}' completed",
                data = new { phase = request.Phase, completed }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // ========== Task Dashboard ==========

    /// <summary>
    /// GET /api/tasks/dashboard - لوحة تحكم المهام
    /// </summary>
    [HttpGet("tasks/dashboard")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetTaskDashboard([FromQuery] int? branchId)
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        int? assigneeId = userRole == UserRole.Employee ? userId : null;
        
        var dashboard = await _taskService.GetTaskDashboardAsync(branchId, assigneeId);
        
        return Ok(new { success = true, data = dashboard });
    }

    /// <summary>
    /// GET /api/tasks/overdue - المهام المتأخرة
    /// </summary>
    [HttpGet("tasks/overdue")]
    public async Task<IActionResult> GetOverdueTasks()
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        IEnumerable<Models.Entities.Task> tasks;
        
        if (userRole == UserRole.Admin)
        {
            tasks = await _taskService.GetOverdueTasksAsync();
        }
        else
        {
            tasks = await _taskService.GetOverdueTasksAsync(userId);
        }

        var taskDtos = tasks.Select(t => new
        {
            t.Id,
            t.Title,
            t.JobOrderId,
            jobNumber = t.JobOrder?.JobNumber,
            t.DueAt,
            daysOverdue = (DateTime.UtcNow - t.DueAt).Days,
            t.AssignedTo,
            assignedToName = t.Assignee?.FullName,
            t.Status
        });

        return Ok(new { success = true, data = taskDtos });
    }

    // ========== Private Helper Methods ==========

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirst("userId")?.Value ?? "0");
    }

    private UserRole GetCurrentUserRole()
    {
        var roleString = User.FindFirst("role")?.Value;
        return Enum.TryParse<UserRole>(roleString, out var role) ? role : UserRole.Customer;
    }

    private async Task<bool> CanAccessJobOrder(int jobOrderId)
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        if (userRole == UserRole.Admin)
            return true;

        var jobOrder = await _jobOrderService.GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            return false;

        if (userRole == UserRole.Employee || userRole == UserRole.Designer || userRole == UserRole.Driver)
        {
            // Check if user is assigned to any task in this job order
            var tasks = await _taskService.GetTasksByJobOrderAsync(jobOrderId);
            return tasks.Any(t => t.AssignedTo == userId);
        }

        if (userRole == UserRole.Customer)
        {
            var order = await _jobOrderService.GetJobOrderByIdAsync(jobOrderId);
            return order?.SourceOrder?.CustomerId == userId;
        }

        return false;
    }
}

// ========== Request/Response DTOs ==========

public class CreateManualTaskRequest
{
    public TaskType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? AssignedTo { get; set; }
    public DateTime? DueAt { get; set; }
    public List<CreateSubTaskRequest>? SubTasks { get; set; }
}

public class CreateSubTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Order { get; set; }
}

public class UpdateTaskRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? DueAt { get; set; }
}

public class AssignTaskRequest
{
    public int AssignedTo { get; set; }
}

public class SkipTaskRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class AddSubTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Order { get; set; }
}

public class UpdateSubTaskStatusRequest
{
    public bool IsCompleted { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
}

public class AddInventoryToTaskRequest
{
    public int InventoryItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal? SellingPricePerUnit { get; set; }
    public bool AddToChecklist { get; set; } = true;
}

public class UpdateChecklistRequest
{
    public int ChecklistItemId { get; set; }
    public bool IsChecked { get; set; }
    public string? ProofImageUrl { get; set; }
}

public class CompletePhaseRequest
{
    public ChecklistPhase Phase { get; set; }
}

public class TaskResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType Type { get; set; }
    public TaskStatus Status { get; set; }
    public DateTime DueAt { get; set; }
    public int JobOrderId { get; set; }
    public string? JobNumber { get; set; }
    public int? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public int? CompletedBy { get; set; }
    public string? CompletedByName { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsOverdue { get; set; }
    public int SubTasksCount { get; set; }
    public int CompletedSubTasksCount { get; set; }
}

public class TaskDetailResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType Type { get; set; }
    public TaskStatus Status { get; set; }
    public DateTime DueAt { get; set; }
    public int? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public string? SkipReason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedBy { get; set; }
    public string? CompletedByName { get; set; }
    public List<SubTaskResponseDto>? SubTasks { get; set; }
    public List<ChecklistItemResponseDto>? ChecklistItems { get; set; }
}

public class SubTaskResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedBy { get; set; }
    public string? CompletedByName { get; set; }
    public int? Order { get; set; }
}

public class ChecklistItemResponseDto
{
    public int Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public ChecklistPhase Phase { get; set; }
    public bool IsChecked { get; set; }
    public DateTime? CheckedAt { get; set; }
    public string? CheckedByName { get; set; }
    public string? ProofImageUrl { get; set; }
}