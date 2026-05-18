using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Implementations;
using Palloncino.Services.Interfaces;
using TaskStatus = Palloncino.Models.Enums.TaskStatus;
namespace Palloncino.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class TaskController(
    ITaskService taskService,
    IJobOrderService jobOrderService,
    IInventoryService inventoryService,
    IUserService userService,
    ILogger<TaskController> logger,
    ApplicationDbContext context,
    INotificationService notificationService,
    IFileStorageService fileStorageService) : ControllerBase
{

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
            tasks = await taskService.GetAllTasksAsync(new TaskFilter { Status = status });
        }
        else
        {
            // Regular users see only their assigned tasks
            tasks = await taskService.GetTasksByAssigneeAsync(userId, status);
        }

        var taskDtos = tasks.Select(t => new TaskResponseDto
        {
            Id = t.Id,
            Title = t.Title ?? "",
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

        var tasks = await taskService.GetTasksByJobOrderAsync(jobOrderId);

        var taskDtos = tasks.Select(t => new TaskDetailResponseDto
        {
            Id = t.Id,
            Title = t.Title ?? "",
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
                ItemName = ci.ItemName ?? "",
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
        var jobOrder = await jobOrderService.GetJobOrderByIdAsync(jobOrderId);
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
            var created = await taskService.CreateTaskAsync(task, subTasks);

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
        var existingTask = await taskService.GetTaskByIdAsync(taskId);
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
            var updated = await taskService.UpdateTaskAsync(existingTask);

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
        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        try
        {
            var updated = await taskService.AssignTaskAsync(taskId, request.AssignedTo, GetCurrentUserId());

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
        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        var userId = GetCurrentUserId();
        var canComplete = await taskService.CanCompleteTaskAsync(taskId, userId);

        if (!canComplete)
            return Forbid();

        try
        {
            Models.Entities.Task updated;

            if (task.AssignedTo != userId)
            {
                // Task completed by someone else - BR-12 requires logging
                await taskService.CompleteTaskForOthersAsync(taskId, userId, task.AssignedTo ?? 0);
                updated = await taskService.GetTaskByIdAsync(taskId)
                ?? throw new Exception("task not found");

                logger.LogWarning("Task {TaskId} was completed by User {CompleterId} instead of assigned user {AssigneeId}",
                    taskId, userId, task.AssignedTo);
            }
            else
            {
                updated = await taskService.CompleteTaskAsync(taskId, userId);
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
        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        var userId = GetCurrentUserId();

        if (task.AssignedTo != userId && GetCurrentUserRole() != UserRole.Admin)
            return Forbid();

        try
        {
            var updated = await taskService.StartTaskAsync(taskId, userId);

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

        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        try
        {
            var updated = await taskService.SkipTaskAsync(taskId, GetCurrentUserId(), request.Reason);

            // Log skip reason in activity log (BR-08)
            logger.LogInformation("Task {TaskId} skipped by User {UserId}. Reason: {Reason}",
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
        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        try
        {
            var subTask = await taskService.AddSubTaskAsync(taskId, request.Title, request.Description, request.Order);

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
        var task = await taskService.GetTaskByIdAsync(taskId);
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
                updated = await taskService.CompleteSubTaskAsync(subTaskId, userId);
            }
            else
            {
                // Update title/description if provided
                updated = await taskService.UpdateSubTaskAsync(subTaskId, request.Title, request.Description);
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
        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        try
        {
            var deleted = await taskService.RemoveSubTaskAsync(subTaskId);

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
        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        // Check if inventory item exists and has sufficient stock
        var hasStock = await inventoryService.HasSufficientStockAsync(request.InventoryItemId, request.Quantity);
        if (!hasStock)
            return BadRequest(new { success = false, message = "Insufficient stock" });

        var inventoryItem = await inventoryService.GetInventoryItemByIdAsync(request.InventoryItemId);
        if (inventoryItem == null)
            return NotFound(new { success = false, message = "Inventory item not found" });

        try
        {
            // Add item to job order (FR-TASK-08)
            var jobOrderItem = await jobOrderService.AddJobOrderItemAsync(
                task.JobOrderId,
                request.InventoryItemId,
                request.Quantity,
                request.SellingPricePerUnit);

            // Add to checklist if needed
            if (request.AddToChecklist)
            {
                var checklistItem = await taskService.AddChecklistItemAsync(
                    taskId,
                    ChecklistPhase.LoadingFromBranch,
                    $"{inventoryItem.Title} (x{request.Quantity})");
            }

            // Mark item as prepared automatically
            await jobOrderService.MarkItemAsPreparedAsync(jobOrderItem.Id, GetCurrentUserId());

            logger.LogInformation("Inventory item {ItemId} added to Task {TaskId} by User {UserId}. Quantity: {Quantity}",
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
        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        try
        {
            var updated = await taskService.UpdateChecklistItemAsync(
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
        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        try
        {
            var completed = await taskService.CompleteChecklistPhaseAsync(taskId, request.Phase, GetCurrentUserId());

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

        var dashboard = await taskService.GetTaskDashboardAsync(branchId, assigneeId);

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
            tasks = await taskService.GetOverdueTasksAsync();
        }
        else
        {
            tasks = await taskService.GetOverdueTasksAsync(userId);
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

    // ========== Design Task Endpoints ==========

    /// <summary>
    /// GET /api/tasks/:id/design - تفاصيل مهمة التصميم + مراجع العميل
    /// </summary>
    [HttpGet("tasks/{taskId}/design")]
    [Authorize(Roles = "Admin,Designer,Employee")]
    public async Task<IActionResult> GetDesignTaskDetails(int taskId)
    {
        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        if (task.Type != TaskType.Design)
            return BadRequest(new { success = false, message = "This is not a design task" });

        // Check authorization
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        if (userRole != UserRole.Admin && task.AssignedTo != userId)
            return Forbid();

        // Get job order for customer references
        var jobOrder = await jobOrderService.GetJobOrderByIdAsync(task.JobOrderId);
        var order = jobOrder?.SourceOrder;

        // Get customer reference images (attachments from the order)
        var referenceAttachments = new List<AttachmentDto>();
        if (order?.Attachments != null)
        {
            referenceAttachments = order.Attachments
                .Where(a => a.Type == AttachmentType.Order)
                .Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName ?? "",
                    FileUrl = a.FileUrl ?? "",
                    FileType = a.FileType?? "",
                    CreatedAt = a.CreatedAt
                }).ToList();
        }

        // Get design proposals uploaded by designer
        var designProposals = await context.Attachments
            .Where(a => a.EntityId == task.Id && a.Type == AttachmentType.Design && !a.IsDeleted)
            .Select(a => new AttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName ?? "",
                FileUrl = a.FileUrl?? "",
                FileType = a.FileType?? "",
                UploadedBy = a.UploadedBy,
                UploadedByName = a.Uploader != null ? a.Uploader.FullName : "",
                CreatedAt = a.CreatedAt,
                Description = a.Description
            })
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var response = new DesignTaskDetailsDto
        {
            TaskId = task.Id,
            TaskTitle = task.Title?? "",
            TaskDescription = task.Description,
            TaskStatus = task.Status,
            DueAt = task.DueAt,
            AssignedTo = task.AssignedTo,
            AssignedToName = task.Assignee?.FullName,
            JobOrderId = task.JobOrderId,
            JobNumber = jobOrder?.JobNumber,
            CustomerId = order?.CustomerId ?? 0,
            CustomerName = order?.Customer?.FullName,
            CustomerPhone = order?.Customer?.Phone,
            CustomerEmail = order?.Customer?.Email,
            DesignBrief = order?.CustomDesignDescription,
            SpecialInstructions = jobOrder?.SpecialInstructions,
            ReferenceImages = referenceAttachments,
            DesignProposals = designProposals,
            StatusHistory = await GetDesignStatusHistory(task.Id)
        };

        return Ok(new { success = true, data = response });
    }

    /// <summary>
    /// POST /api/tasks/:id/design/uploads - رفع مقترح تصميم
    /// </summary>
    [HttpPost("tasks/{taskId}/design/uploads")]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<IActionResult> UploadDesignProposal(int taskId, [FromForm] UploadDesignProposalRequest request)
    {
        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        if (task.Type != TaskType.Design)
            return BadRequest(new { success = false, message = "This is not a design task" });

        // Check authorization
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        if (userRole != UserRole.Admin && task.AssignedTo != userId)
            return Forbid();

        if (request.Files == null || !request.Files.Any())
            return BadRequest(new { success = false, message = "At least one file is required" });

        var uploadedFiles = new List<AttachmentDto>();

        foreach (var file in request.Files)
        {
            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".ai", ".psd" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest(new { success = false, message = $"File type {fileExtension} is not allowed" });

            // Validate file size (max 20MB for design files)
            if (file.Length > 20 * 1024 * 1024)
                return BadRequest(new { success = false, message = $"File {file.FileName} exceeds 20MB limit" });

            // Upload file
            var fileUrl = await fileStorageService.UploadFileAsync(file, $"designs/task_{taskId}");

            var attachment = new Attachment
            {
                EntityId = taskId,
                Type = AttachmentType.Design,
                FileName = file.FileName,
                FileUrl = fileUrl,
                FileType = file.ContentType,
                FileSize = file.Length,
                UploadedBy = userId,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow
            };

            context.Attachments.Add(attachment);
            await context.SaveChangesAsync();

            uploadedFiles.Add(new AttachmentDto
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                FileUrl = attachment.FileUrl,
                FileType = attachment.FileType,
                CreatedAt = attachment.CreatedAt
            });
        }

        logger.LogInformation("Designer {UserId} uploaded {FileCount} design proposal(s) for Task {TaskId}",
            userId, uploadedFiles.Count, taskId);

        return Ok(new
        {
            success = true,
            message = "Design proposal uploaded successfully",
            data = uploadedFiles
        });
    }

    /// <summary>
    /// PUT /api/tasks/:id/design/status - تحديث الحالة: in_progress | pending_review | completed
    /// </summary>
    [HttpPut("tasks/{taskId}/design/status")]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<IActionResult> UpdateDesignTaskStatus(int taskId, [FromBody] UpdateDesignStatusRequest request)
    {
        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        if (task.Type != TaskType.Design)
            return BadRequest(new { success = false, message = "This is not a design task" });

        // Validate status
        var allowedStatuses = new[] { "in_progress", "pending_review", "completed" };
        if (!allowedStatuses.Contains(request.Status))
            return BadRequest(new { success = false, message = "Invalid status. Allowed: in_progress, pending_review, completed" });

        // Check authorization
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        if (userRole != UserRole.Admin && task.AssignedTo != userId)
            return Forbid();

        var newStatus = request.Status switch
        {
            "in_progress" => TaskStatus.InProgress,
            "pending_review" => TaskStatus.Pending, // Using Pending for review status
            "completed" => TaskStatus.Completed,
            _ => TaskStatus.Pending
        };

        try
        {
            // Update task status
            var updatedTask = await taskService.UpdateTaskStatusAsync(taskId, newStatus, userId, null);

            // Create status history record (optional - for tracking)
            var statusHistory = new DesignStatusHistory
            {
                TaskId = taskId,
                PreviousStatus = task.Status.ToString(),
                NewStatus = updatedTask.Status.ToString(),
                ChangedBy = userId,
                ChangedAt = DateTime.UtcNow,
                Notes = request.Notes
            };
            context.DesignStatusHistories.Add(statusHistory);
            await context.SaveChangesAsync();

            // Send notification to admin when design is ready for review
            if (request.Status == "pending_review")
            {
                await NotifyAdminDesignReadyForReview(taskId, userId);
            }

            logger.LogInformation("Design task {TaskId} status updated to {NewStatus} by User {UserId}",
                taskId, request.Status, userId);

            return Ok(new
            {
                success = true,
                message = $"Design task status updated to {request.Status}",
                data = new
                {
                    taskId = updatedTask.Id,
                    status = request.Status,
                    updatedAt = DateTime.UtcNow
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/tasks/design/pending - المهام التصميمية المنتظرة للمراجعة (للمصممين)
    /// </summary>
    [HttpGet("tasks/design/pending")]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<IActionResult> GetPendingDesignTasks()
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        IEnumerable<Models.Entities.Task> tasks;

        if (userRole == UserRole.Admin)
        {
            tasks = await taskService.GetTasksByTypeAsync(TaskType.Design, null);
            tasks = tasks.Where(t => t.Status == TaskStatus.Pending);
        }
        else
        {
            tasks = await taskService.GetTasksByAssigneeAsync(userId, TaskStatus.Pending);
            tasks = tasks.Where(t => t.Type == TaskType.Design);
        }

        var taskDtos = tasks.Select(t => new PendingDesignTaskDto
        {
            TaskId = t.Id,
            TaskTitle = t.Title?? "",
            JobOrderId = t.JobOrderId,
            JobNumber = t.JobOrder?.JobNumber,
            CustomerName = t.JobOrder?.SourceOrder?.Customer?.FullName,
            DueAt = t.DueAt,
            DaysRemaining = (t.DueAt - DateTime.UtcNow).Days,
            HasProposals = context.Attachments.Any(a => a.EntityId == t.Id && a.Type == AttachmentType.Design)
        });

        return Ok(new { success = true, data = taskDtos });
    }

    /// <summary>
    /// GET /api/tasks/design/review - المهام التصميمية الجاهزة للمراجعة (للأدمن)
    /// </summary>
    [HttpGet("tasks/design/review")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetDesignTasksReadyForReview()
    {
        var tasks = await taskService.GetTasksByTypeAsync(TaskType.Design, null);

        // Tasks that are pending review (status = Pending but have proposals)
        var pendingReviewTasks = tasks.Where(t =>
            t.Status == TaskStatus.Pending &&
            context.Attachments.Any(a => a.EntityId == t.Id && a.Type == AttachmentType.Design));

        var taskDtos = pendingReviewTasks.Select(t => new DesignReviewTaskDto
        {
            TaskId = t.Id,
            TaskTitle = t.Title?? "",
            JobOrderId = t.JobOrderId,
            JobNumber = t.JobOrder?.JobNumber,
            CustomerName = t.JobOrder?.SourceOrder?.Customer?.FullName,
            DesignerName = t.Assignee?.FullName,
            DueAt = t.DueAt,
            ProposalCount = context.Attachments.Count(a => a.EntityId == t.Id && a.Type == AttachmentType.Design),
            LatestProposalAt = context.Attachments
                .Where(a => a.EntityId == t.Id && a.Type == AttachmentType.Design)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => a.CreatedAt)
                .FirstOrDefault()
        });

        return Ok(new { success = true, data = taskDtos });
    }

    /// <summary>
    /// POST /api/tasks/:id/design/feedback - إضافة تعليق على التصميم (للعميل أو الأدمن)
    /// </summary>
    [HttpPost("tasks/{taskId}/design/feedback")]
    [Authorize]
    public async Task<IActionResult> AddDesignFeedback(int taskId, [FromBody] AddDesignFeedbackRequest request)
    {
        var task = await taskService.GetTaskByIdAsync(taskId);
        if (task == null)
            return NotFound(new { success = false, message = "Task not found" });

        if (task.Type != TaskType.Design)
            return BadRequest(new { success = false, message = "This is not a design task" });

        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        // Check if user is authorized (Admin, Designer assigned to task, or customer who owns the order)
        var isAuthorized = false;

        if (userRole == UserRole.Admin)
            isAuthorized = true;
        else if (userRole == UserRole.Designer && task.AssignedTo == userId)
            isAuthorized = true;
        else if (userRole == UserRole.Customer)
        {
            var jobOrder = await jobOrderService.GetJobOrderByIdAsync(task.JobOrderId);
            if (jobOrder?.SourceOrder?.CustomerId == userId)
                isAuthorized = true;
        }

        if (!isAuthorized)
            return Forbid();

        // Save feedback as chat message in the task chat
        var chatMessage = new ChatMessage
        {
            RoomType = ChatRoomType.Task,
            RoomId = taskId,
            SenderId = userId,
            Message = request.Feedback,
            ImageUrl = request.ImageUrl,
            CreatedAt = DateTime.UtcNow
        };

        context.ChatMessages.Add(chatMessage);
        await context.SaveChangesAsync();

        // If feedback is from admin/customer, notify designer
        if (userRole != UserRole.Designer)
        {
            await notificationService.SendInternalNotificationAsync(
                task.AssignedTo ?? 0,
                "New Design Feedback",
                $"New feedback on design task '{task.Title}': {request.Feedback[..Math.Min(100, request.Feedback.Length)]}...",
                NotificationType.TaskAssigned,
                taskId,
                "Task");
        }

        logger.LogInformation("Design feedback added for Task {TaskId} by User {UserId}", taskId, userId);

        return Ok(new
        {
            success = true,
            message = "Feedback added successfully",
            data = new { chatMessage.Id, chatMessage.Message, sentAt = chatMessage.CreatedAt }
        });
    }

    // ========== Private Helper Methods for Design Tasks ==========

    private async Task<List<StatusHistoryDto>> GetDesignStatusHistory(int taskId)
    {
        var history = await context.DesignStatusHistories
            .Where(h => h.TaskId == taskId)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new StatusHistoryDto
            {
                PreviousStatus = h.PreviousStatus,
                NewStatus = h.NewStatus,
                ChangedBy = h.ChangedBy,
                ChangedByName = context.Users.Where(u => u.Id == h.ChangedBy).Select(u => u.FullName).FirstOrDefault(),
                ChangedAt = h.ChangedAt,
                Notes = h.Notes
            })
            .ToListAsync();

        return history;
    }

    private async System.Threading.Tasks.Task NotifyAdminDesignReadyForReview(int taskId, int designerId)
    {
        var task = await taskService.GetTaskByIdAsync(taskId);
        var designer = await userService.GetUserByIdAsync(designerId);

        var admins = await userService.GetUsersByRoleAsync(UserRole.Admin);

        foreach (var admin in admins)
        {
            await notificationService.SendInternalNotificationAsync(
                admin.Id,
                "🎨 Design Ready for Review",
                $"Designer {designer?.FullName} has completed the design for task '{task?.Title}'. Please review.",
                NotificationType.Alert,
                taskId,
                "Task");
        }
    }

    // ========== Private Helper Methods ==========

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirst("userId")?.Value ?? "0");
    }

    private UserRole GetCurrentUserRole()
    {
        var roleString = User.FindFirst(u=>u.Type == ClaimTypes.Role)?.Value;
        return Enum.TryParse<UserRole>(roleString, out var role) ? role : UserRole.Customer;
    }

    private async Task<bool> CanAccessJobOrder(int jobOrderId)
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        if (userRole == UserRole.Admin)
            return true;

        var jobOrder = await jobOrderService.GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            return false;

        if (userRole == UserRole.Employee || userRole == UserRole.Designer || userRole == UserRole.Driver)
        {
            // Check if user is assigned to any task in this job order
            var tasks = await taskService.GetTasksByJobOrderAsync(jobOrderId);
            return tasks.Any(t => t.AssignedTo == userId);
        }

        if (userRole == UserRole.Customer)
        {
            var order = await jobOrderService.GetJobOrderByIdAsync(jobOrderId);
            return order?.SourceOrder?.CustomerId == userId;
        }

        return false;
    }
}
