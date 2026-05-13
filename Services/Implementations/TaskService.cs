using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;
using TaskStatus = Palloncino.Models.Enums.TaskStatus;

namespace Palloncino.Services.Implementations;

public class TaskService(
    ApplicationDbContext context,
    ILogger<TaskService> logger,
    INotificationService notificationService) : ITaskService
{
    // ========== CRUD Operations ==========
    
    public async Task<Models.Entities.Task> CreateTaskAsync(Models.Entities.Task task, List<CreateSubTaskDto>? subTasks = null)
    {
        // Validate job order exists
        var jobOrder = await context.JobOrders
            .FirstOrDefaultAsync(j => j.Id == task.JobOrderId && !j.IsDeleted);
        
        if (jobOrder == null)
            throw new InvalidOperationException($"JobOrder with ID {task.JobOrderId} not found");
        
        // Validate assignee if provided
        if (task.AssignedTo.HasValue)
        {
            var assignee = await context.Users
                .FirstOrDefaultAsync(u => u.Id == task.AssignedTo.Value && u.Status == UserStatus.Active && !u.IsDeleted);
            
            if (assignee == null)
                throw new InvalidOperationException($"User with ID {task.AssignedTo.Value} not found");
            
            // Validate role matches task type
            if (!IsValidAssigneeForTaskType(task.Type, assignee.Role))
                throw new InvalidOperationException($"User with role {assignee.Role} cannot be assigned to {task.Type} task");
        }
        
        // Set default due date if not provided
        if (task.DueAt == default)
        {
            task.DueAt = task.Type switch
            {
                TaskType.Preparation => jobOrder.DueAt.AddHours(-4),
                TaskType.Design => jobOrder.DueAt.AddHours(-24),
                TaskType.Delivery => jobOrder.DueAt,
                _ => jobOrder.DueAt
            };
        }
        
        task.Status = TaskStatus.Pending;
        task.CreatedAt = DateTime.UtcNow;
        task.IsActive = true;
        
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        
        // Add sub-tasks if provided
        if (subTasks != null && subTasks.Any())
        {
            int order = 1;
            foreach (var subTaskDto in subTasks)
            {
                var subTask = new SubTask
                {
                    TaskId = task.Id,
                    Title = subTaskDto.Title,
                    Description = subTaskDto.Description,
                    Order = subTaskDto.Order ?? order++,
                    IsCompleted = false,
                    CreatedAt = DateTime.UtcNow
                };
                context.SubTasks.Add(subTask);
            }
            await context.SaveChangesAsync();
        }
        
        // Send notification to assignee (FR-NOT-04)
        if (task.AssignedTo.HasValue)
        {
            await notificationService.SendTaskAssignedNotification(task.Id, task.AssignedTo.Value);
        }
        
        logger.LogInformation("Task created: {TaskTitle} (ID: {TaskId}) for JobOrder {JobOrderId}", 
            task.Title, task.Id, task.JobOrderId);
        
        return task;
    }
    
    public async Task<Models.Entities.Task> UpdateTaskAsync(Models.Entities.Task task)
    {
        var existingTask = await GetTaskByIdAsync(task.Id);
        if (existingTask == null)
            throw new InvalidOperationException($"Task with ID {task.Id} not found");
        
        // Cannot update completed or skipped tasks
        if (existingTask.Status == TaskStatus.Completed || existingTask.Status == TaskStatus.Skipped)
            throw new InvalidOperationException($"Cannot update {existingTask.Status} task");
        
        // Update fields
        existingTask.Title = task.Title;
        existingTask.Description = task.Description;
        existingTask.DueAt = task.DueAt;
        existingTask.UpdatedAt = DateTime.UtcNow;
        existingTask.UpdatedBy = task.UpdatedBy;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Task updated: {TaskTitle} (ID: {TaskId})", existingTask.Title, task.Id);
        
        return existingTask;
    }
    
    public async Task<bool> DeleteTaskAsync(int taskId)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            return false;
        
        // Cannot delete completed or in-progress tasks
        if (task.Status == TaskStatus.Completed)
            throw new InvalidOperationException("Cannot delete completed task");
        
        if (task.Status == TaskStatus.InProgress)
            throw new InvalidOperationException("Cannot delete task that is in progress");
        
        // Remove sub-tasks and checklist items
        var subTasks = context.SubTasks.Where(st => st.TaskId == taskId);
        context.SubTasks.RemoveRange(subTasks);
        
        var checklistItems = context.ChecklistItems.Where(ci => ci.TaskId == taskId);
        context.ChecklistItems.RemoveRange(checklistItems);
        
        context.Tasks.Remove(task);
        await context.SaveChangesAsync();
        
        logger.LogWarning("Task permanently deleted: {TaskTitle} (ID: {TaskId})", task.Title, taskId);
        
        return true;
    }
    
    public async Task<bool> SoftDeleteTaskAsync(int taskId, int deletedBy)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            return false;
        
        task.SoftDelete(deletedBy);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Task soft deleted: {TaskTitle} (ID: {TaskId}) by User {DeletedBy}", 
            task.Title, taskId, deletedBy);
        
        return true;
    }
    
    // ========== Queries ==========
    
    public async Task<Models.Entities.Task?> GetTaskByIdAsync(int taskId)
    {
        return await context.Tasks
            .Include(t => t.JobOrder)
                .ThenInclude(j => j != null ? j.Branch : null)
            .Include(t => t.Assignee)
            .Include(t => t.Completer)
            .Include(t => t.SubTasks)
                .ThenInclude(st => st.Completer)
            .Include(t => t.ChecklistItems)
            .FirstOrDefaultAsync(t => t.Id == taskId && !t.IsDeleted);
    }
    
    public async Task<IEnumerable<Models.Entities.Task>> GetAllTasksAsync(TaskFilter? filter = null)
    {
        var query = context.Tasks
            .Include(t => t.JobOrder)
            .Include(t => t.Assignee)
            .Where(t => !t.IsDeleted);
        
        if (filter != null)
        {
            if (filter.Type.HasValue)
                query = query.Where(t => t.Type == filter.Type.Value);
            
            if (filter.Status.HasValue)
                query = query.Where(t => t.Status == filter.Status.Value);
            
            if (filter.JobOrderId.HasValue)
                query = query.Where(t => t.JobOrderId == filter.JobOrderId.Value);
            
            if (filter.AssigneeId.HasValue)
                query = query.Where(t => t.AssignedTo == filter.AssigneeId.Value);
            
            if (filter.BranchId.HasValue)
                query = query.Where(t => t.JobOrder != null && t.JobOrder.BranchId == filter.BranchId.Value);
            
            if (filter.FromDueDate.HasValue)
                query = query.Where(t => t.DueAt >= filter.FromDueDate.Value);
            
            if (filter.ToDueDate.HasValue)
                query = query.Where(t => t.DueAt <= filter.ToDueDate.Value);
            
            if (!filter.IncludeCompleted)
                query = query.Where(t => t.Status != TaskStatus.Completed && t.Status != TaskStatus.Skipped);
        }
        
        return await query
            .OrderBy(t => t.DueAt)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Models.Entities.Task>> GetTasksByJobOrderAsync(int jobOrderId)
    {
        return await context.Tasks
            .Include(t => t.Assignee)
            .Include(t => t.SubTasks)
            .Include(t => t.ChecklistItems)
            .Where(t => t.JobOrderId == jobOrderId && !t.IsDeleted)
            .OrderBy(t => t.DueAt)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Models.Entities.Task>> GetTasksByAssigneeAsync(int assigneeId, TaskStatus? status = null)
    {
        var query = context.Tasks
            .Include(t => t.JobOrder)
            .Where(t => t.AssignedTo == assigneeId && !t.IsDeleted);
        
        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);
        
        return await query
            .OrderBy(t => t.DueAt)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Models.Entities.Task>> GetTasksByTypeAsync(TaskType type, int? branchId = null)
    {
        var query = context.Tasks
            .Include(t => t.JobOrder)
            .Where(t => t.Type == type && !t.IsDeleted);
        
        if (branchId.HasValue)
            query = query.Where(t => t.JobOrder != null && t.JobOrder.BranchId == branchId.Value);
        
        return await query
            .OrderBy(t => t.DueAt)
            .ToListAsync();
    }
    
    // ========== Status Management ==========
    
    public async Task<Models.Entities.Task> UpdateTaskStatusAsync(int taskId, TaskStatus status, int updatedBy, string? skipReason = null)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");
        
        // Validate status transition
        if (!IsValidStatusTransition(task.Status, status))
            throw new InvalidOperationException($"Invalid status transition from {task.Status} to {status}");
        
        // Validate skip reason
        if (status == TaskStatus.Skipped && string.IsNullOrWhiteSpace(skipReason))
            throw new InvalidOperationException("Skip reason is required when skipping a task");
        
        var oldStatus = task.Status;
        task.Status = status;
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedBy = updatedBy;
        
        if (status == TaskStatus.Skipped)
            task.SkipReason = skipReason;
        
        if (status == TaskStatus.Completed)
        {
            task.CompletedAt = DateTime.UtcNow;
            task.CompletedBy = updatedBy;
        }
        
        await context.SaveChangesAsync();
        
        // Log status change for BR-12 (activity log)
        logger.LogInformation("Task {TaskId} status changed from {OldStatus} to {NewStatus} by User {UpdatedBy}. SkipReason: {SkipReason}", 
            taskId, oldStatus, status, updatedBy, skipReason ?? "N/A");
        
        // Send notification (FR-NOT-04)
        if (status == TaskStatus.Completed && task.AssignedTo.HasValue && task.AssignedTo.Value != updatedBy)
        {
            await notificationService.SendTaskCompletedByOtherNotification(task.Id, task.AssignedTo.Value, updatedBy);
        }
        
        return task;
    }
    
    public async Task<Models.Entities.Task> StartTaskAsync(int taskId, int startedBy)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");
        
        if (task.Status != TaskStatus.Pending)
            throw new InvalidOperationException($"Cannot start task with status {task.Status}");
        
        task.Status = TaskStatus.InProgress;
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedBy = startedBy;
        
        await context.SaveChangesAsync();
        
        logger.LogDebug("Task started: {TaskTitle} (ID: {TaskId}) by User {StartedBy}", 
            task.Title, taskId, startedBy);
        
        return task;
    }
    
    public async Task<Models.Entities.Task> CompleteTaskAsync(int taskId, int completedBy)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");
        
        // Check all sub-tasks are completed
        var incompleteSubTasks = await context.SubTasks
            .AnyAsync(st => st.TaskId == taskId && !st.IsCompleted);
        
        if (incompleteSubTasks)
            throw new InvalidOperationException("Cannot complete task with incomplete sub-tasks");
        
        return await UpdateTaskStatusAsync(taskId, TaskStatus.Completed, completedBy);
    }
    
    public async Task<Models.Entities.Task> SkipTaskAsync(int taskId, int skippedBy, string reason)
    {
        return await UpdateTaskStatusAsync(taskId, TaskStatus.Skipped, skippedBy, reason);
    }
    
    // ========== Assignment Management ==========
    
    public async Task<Models.Entities.Task> AssignTaskAsync(int taskId, int assignedTo, int assignedBy)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");
        
        var assignee = await context.Users
            .FirstOrDefaultAsync(u => u.Id == assignedTo && u.Status == UserStatus.Active && !u.IsDeleted);
        
        if (assignee == null)
            throw new InvalidOperationException($"User with ID {assignedTo} not found");
        
        // Validate role matches task type
        if (!IsValidAssigneeForTaskType(task.Type, assignee.Role))
            throw new InvalidOperationException($"User with role {assignee.Role} cannot be assigned to {task.Type} task");
        
        task.AssignedTo = assignedTo;
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedBy = assignedBy;
        
        await context.SaveChangesAsync();
        
        // Send notification
        await notificationService.SendTaskAssignedNotification(taskId, assignedTo);
        
        logger.LogInformation("Task {TaskId} assigned to User {AssignedTo} by User {AssignedBy}", 
            taskId, assignedTo, assignedBy);
        
        return task;
    }
    
    public async Task<Models.Entities.Task> ReassignTaskAsync(int taskId, int newAssigneeId, int reassignedBy, string? reason = null)
    {
        var oldAssignee = await context.Tasks
            .Where(t => t.Id == taskId)
            .Select(t => t.AssignedTo)
            .FirstOrDefaultAsync();
        
        var task = await AssignTaskAsync(taskId, newAssigneeId, reassignedBy);
        
        // Log reassignment
        logger.LogInformation("Task {TaskId} reassigned from User {OldAssignee} to User {NewAssignee} by User {ReassignedBy}. Reason: {Reason}", 
            taskId, oldAssignee, newAssigneeId, reassignedBy, reason ?? "N/A");
        
        return task;
    }
    
    public async Task<bool> CompleteTaskForOthersAsync(int taskId, int completedBy, int originalAssigneeId)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            return false;
        
        if (task.Status == TaskStatus.Completed)
            throw new InvalidOperationException("Task is already completed");
        
        // Check if user is allowed to complete task for others (BR-12)
        var currentUser = await context.Users.FindAsync(completedBy);
        
        // Admin, Employee, or Manager can complete tasks for others
        if (currentUser?.Role != UserRole.Admin && currentUser?.Role != UserRole.Employee)
            throw new InvalidOperationException("You are not authorized to complete tasks for others");
        
        // Complete the task
        task.Status = TaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        task.CompletedBy = completedBy;
        task.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        // Log that task was completed by someone else (BR-12 requires explicit logging)
        logger.LogWarning("Task {TaskId} was completed by User {CompletedBy} instead of assigned user {OriginalAssignee}", 
            taskId, completedBy, originalAssigneeId);
        
        // Notify original assignee
        await notificationService.SendTaskCompletedByOtherNotification(taskId, originalAssigneeId, completedBy);
        
        return true;
    }
    
    // ========== SubTask Management ==========
    
    public async Task<SubTask> AddSubTaskAsync(int taskId, string title, string? description = null, int? order = null)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");
        
        var maxOrder = await context.SubTasks
            .Where(st => st.TaskId == taskId)
            .MaxAsync(st => (int?)st.Order) ?? 0;
        
        var subTask = new SubTask
        {
            TaskId = taskId,
            Title = title,
            Description = description,
            Order = order ?? maxOrder + 1,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };
        
        context.SubTasks.Add(subTask);
        await context.SaveChangesAsync();
        
        logger.LogDebug("SubTask added to Task {TaskId}: {SubTaskTitle}", taskId, title);
        
        return subTask;
    }
    
    public async Task<SubTask> UpdateSubTaskAsync(int subTaskId, string? title = null, string? description = null)
    {
        var subTask = await context.SubTasks
            .Include(st => st.Task)
            .FirstOrDefaultAsync(st => st.Id == subTaskId);
        
        if (subTask == null)
            throw new InvalidOperationException($"SubTask with ID {subTaskId} not found");
        
        if (subTask.Task.Status == TaskStatus.Completed)
            throw new InvalidOperationException("Cannot update sub-task of completed task");
        
        if (title != null)
            subTask.Title = title;
        
        if (description != null)
            subTask.Description = description;
        
        subTask.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        return subTask;
    }
    
    public async Task<bool> RemoveSubTaskAsync(int subTaskId)
    {
        var subTask = await context.SubTasks
            .Include(st => st.Task)
            .FirstOrDefaultAsync(st => st.Id == subTaskId);
        
        if (subTask == null)
            return false;
        
        if (subTask.Task.Status == TaskStatus.Completed)
            throw new InvalidOperationException("Cannot remove sub-task from completed task");
        
        context.SubTasks.Remove(subTask);
        await context.SaveChangesAsync();
        
        logger.LogDebug("SubTask removed: {SubTaskId}", subTaskId);
        
        return true;
    }
    
    public async Task<SubTask> CompleteSubTaskAsync(int subTaskId, int completedBy)
    {
        var subTask = await context.SubTasks
            .Include(st => st.Task)
            .FirstOrDefaultAsync(st => st.Id == subTaskId);
        
        if (subTask == null)
            throw new InvalidOperationException($"SubTask with ID {subTaskId} not found");
        
        if (subTask.Task.Status == TaskStatus.Completed)
            throw new InvalidOperationException("Cannot complete sub-task of completed task");
        
        subTask.IsCompleted = true;
        subTask.CompletedAt = DateTime.UtcNow;
        subTask.CompletedBy = completedBy;
        subTask.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        // Check if all sub-tasks are completed to auto-complete parent task?
        var allCompleted = await context.SubTasks
            .Where(st => st.TaskId == subTask.TaskId)
            .AllAsync(st => st.IsCompleted);
        
        if (allCompleted && subTask.Task.Status != TaskStatus.Completed)
        {
            logger.LogInformation("All sub-tasks completed for Task {TaskId}, auto-completing task", subTask.TaskId);
            await CompleteTaskAsync(subTask.TaskId, completedBy);
        }
        
        logger.LogDebug("SubTask completed: {SubTaskTitle} by User {CompletedBy}", subTask.Title, completedBy);
        
        return subTask;
    }
    
    public async Task<IEnumerable<SubTask>> GetSubTasksAsync(int taskId)
    {
        return await context.SubTasks
            .Include(st => st.Completer)
            .Where(st => st.TaskId == taskId)
            .OrderBy(st => st.Order)
            .ToListAsync();
    }
    
    // ========== Checklist Management ==========
    
    public async Task<ChecklistItem> AddChecklistItemAsync(int taskId, ChecklistPhase phase, string itemName)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");
        
        var checklistItem = new ChecklistItem
        {
            TaskId = taskId,
            Phase = phase,
            ItemName = itemName,
            IsChecked = false,
            CreatedAt = DateTime.UtcNow
        };
        
        context.ChecklistItems.Add(checklistItem);
        await context.SaveChangesAsync();
        
        return checklistItem;
    }
    
    public async Task<ChecklistItem> UpdateChecklistItemAsync(int checklistItemId, bool isChecked, int checkedBy, string? proofImageUrl = null)
    {
        var checklistItem = await context.ChecklistItems
            .Include(ci => ci.Task)
            .FirstOrDefaultAsync(ci => ci.Id == checklistItemId);
        
        if (checklistItem == null)
            throw new InvalidOperationException($"ChecklistItem with ID {checklistItemId} not found");
        
        checklistItem.IsChecked = isChecked;
        
        if (isChecked)
        {
            checklistItem.CheckedAt = DateTime.UtcNow;
            checklistItem.CheckedBy = checkedBy;
        }
        
        if (!string.IsNullOrEmpty(proofImageUrl))
            checklistItem.ProofImageUrl = proofImageUrl;
        
        checklistItem.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        logger.LogDebug("Checklist item {ChecklistItemId} updated: IsChecked={IsChecked} by User {CheckedBy}", 
            checklistItemId, isChecked, checkedBy);
        
        return checklistItem;
    }
    
    public async Task<bool> RemoveChecklistItemAsync(int checklistItemId)
    {
        var checklistItem = await context.ChecklistItems
            .FirstOrDefaultAsync(ci => ci.Id == checklistItemId);
        
        if (checklistItem == null)
            return false;
        
        context.ChecklistItems.Remove(checklistItem);
        await context.SaveChangesAsync();
        
        return true;
    }
    
    public async Task<IEnumerable<ChecklistItem>> GetChecklistItemsAsync(int taskId, ChecklistPhase? phase = null)
    {
        var query = context.ChecklistItems
            .Where(ci => ci.TaskId == taskId);
        
        if (phase.HasValue)
            query = query.Where(ci => ci.Phase == phase.Value);
        
        return await query
            .OrderBy(ci => ci.Id)
            .ToListAsync();
    }
    
    public async Task<bool> CompleteChecklistPhaseAsync(int taskId, ChecklistPhase phase, int completedBy)
    {
        var items = await context.ChecklistItems
            .Where(ci => ci.TaskId == taskId && ci.Phase == phase)
            .ToListAsync();
        
        if (!items.Any())
            return false;
        
        foreach (var item in items)
        {
            item.IsChecked = true;
            item.CheckedAt = DateTime.UtcNow;
            item.CheckedBy = completedBy;
        }
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Checklist phase {Phase} completed for Task {TaskId} by User {CompletedBy}", 
            phase, taskId, completedBy);
        
        return true;
    }
    
    // ========== Validation ==========
    
    public async Task<bool> TaskExistsAsync(int taskId)
    {
        return await context.Tasks
            .AnyAsync(t => t.Id == taskId && !t.IsDeleted);
    }
    
    public async Task<bool> IsTaskOverdueAsync(int taskId)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            return false;
        
        return task.DueAt < DateTime.UtcNow && task.Status != TaskStatus.Completed && task.Status != TaskStatus.Skipped;
    }
    
    public async Task<IEnumerable<Models.Entities.Task>> GetOverdueTasksAsync(int? assigneeId = null)
    {
        var query = context.Tasks
            .Include(t => t.JobOrder)
            .Where(t => t.DueAt < DateTime.UtcNow 
                && t.Status != TaskStatus.Completed 
                && t.Status != TaskStatus.Skipped 
                && !t.IsDeleted);
        
        if (assigneeId.HasValue)
            query = query.Where(t => t.AssignedTo == assigneeId.Value);
        
        return await query
            .OrderBy(t => t.DueAt)
            .ToListAsync();
    }
    
    // ========== Business Logic ==========
    
    public async Task<Models.Entities.Task> AutoAssignTaskAsync(int taskId)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");
        
        // Find available assignee based on task type
        var availableUser = await FindAvailableAssigneeForTaskType(task.Type, task.JobOrder?.BranchId);
        
        if (availableUser == null)
            throw new InvalidOperationException($"No available assignee found for task type {task.Type}");
        
        task.AssignedTo = availableUser.Id;
        task.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Task {TaskId} auto-assigned to User {AssigneeId}", taskId, availableUser.Id);
        
        return task;
    }
    
    public async Task<bool> CanCompleteTaskAsync(int taskId, int userId)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            return false;
        
        // Assigned user can complete
        if (task.AssignedTo == userId)
            return true;
        
        // Admin can complete any task
        var user = await context.Users.FindAsync(userId);
        if (user?.Role == UserRole.Admin)
            return true;
        
        // Employee can complete tasks in same branch (BR-12)
        if (user?.Role == UserRole.Employee && task.JobOrder != null && task.JobOrder.BranchId == user.BranchId)
            return true;
        
        return false;
    }
    
    public async Task<TaskStatisticsDto> GetTaskStatisticsAsync(int taskId)
    {
        var task = await GetTaskByIdAsync(taskId);
        if (task == null)
            throw new InvalidOperationException($"Task with ID {taskId} not found");
        
        var subTasks = await GetSubTasksAsync(taskId);
        var checklistItems = await GetChecklistItemsAsync(taskId);
        
        var completedSubTasks = subTasks.Count(st => st.IsCompleted);
        var completedChecklist = checklistItems.Count(ci => ci.IsChecked);
        
        var completionPercentage = 0.0;
        
        if (subTasks.Any() || checklistItems.Any())
        {
            var totalItems = subTasks.Count() + checklistItems.Count();
            var completedItems = completedSubTasks + completedChecklist;
            completionPercentage = totalItems > 0 ? (completedItems * 100.0 / totalItems) : 0;
        }
        
        return new TaskStatisticsDto
        {
            TaskId = taskId,
            TaskTitle = task.Title,
            Type = task.Type,
            TotalSubTasks = subTasks.Count(),
            CompletedSubTasks = completedSubTasks,
            TotalChecklistItems = checklistItems.Count(),
            CompletedChecklistItems = completedChecklist,
            CompletionPercentage = completionPercentage,
            TimeSpent = task.CompletedAt.HasValue ? task.CompletedAt.Value - task.CreatedAt : null,
            IsOverdue = task.DueAt < DateTime.UtcNow && task.Status != TaskStatus.Completed,
            AssignedToName = task.Assignee?.FullName,
            CompletedByName = task.Completer?.FullName
        };
    }
    
    // ========== Dashboard & Counters ==========
    
    public async Task<TaskDashboardDto> GetTaskDashboardAsync(int? branchId = null, int? assigneeId = null)
    {
        var query = context.Tasks
            .Include(t => t.JobOrder)
            .Where(t => !t.IsDeleted);
        
        if (branchId.HasValue)
            query = query.Where(t => t.JobOrder != null && t.JobOrder.BranchId == branchId.Value);
        
        if (assigneeId.HasValue)
            query = query.Where(t => t.AssignedTo == assigneeId.Value);
        
        var tasks = await query.ToListAsync();
        
        // Count tasks completed by others (BR-12 tracking)
        var completedByOthers = tasks.Count(t => t.CompletedBy.HasValue && t.AssignedTo.HasValue && t.CompletedBy.Value != t.AssignedTo.Value);
        
        var upcomingTasks = tasks
            .Where(t => t.Status != TaskStatus.Completed && t.Status != TaskStatus.Skipped)
            .OrderBy(t => t.DueAt)
            .Take(5)
            .Select(t => new UpcomingTaskDto
            {
                TaskId = t.Id,
                TaskTitle = t.Title,
                JobNumber = t.JobOrder?.JobNumber ?? "N/A",
                DueAt = t.DueAt,
                Type = t.Type,
                AssignedToName = t.Assignee?.FullName ?? "Unassigned"
            }).ToList();
        
        return new TaskDashboardDto
        {
            TotalTasks = tasks.Count,
            PendingTasks = tasks.Count(t => t.Status == TaskStatus.Pending),
            InProgressTasks = tasks.Count(t => t.Status == TaskStatus.InProgress),
            CompletedTasks = tasks.Count(t => t.Status == TaskStatus.Completed),
            OverdueTasks = tasks.Count(t => t.DueAt < DateTime.UtcNow && t.Status != TaskStatus.Completed && t.Status != TaskStatus.Skipped),
            SkippedTasks = tasks.Count(t => t.Status == TaskStatus.Skipped),
            CompletedByOthers = completedByOthers,
            UpcomingDeadlines = upcomingTasks        };
    }
    
    public async Task<int> GetPendingTasksCountAsync(int? assigneeId = null)
    {
        var query = context.Tasks
            .Where(t => t.Status == TaskStatus.Pending && !t.IsDeleted);
        
        if (assigneeId.HasValue)
            query = query.Where(t => t.AssignedTo == assigneeId.Value);
        
        return await query.CountAsync();
    }
    
    public async Task<int> GetOverdueTasksCountAsync(int? assigneeId = null)
    {
        var query = context.Tasks
            .Where(t => t.DueAt < DateTime.UtcNow 
                && t.Status != TaskStatus.Completed 
                && t.Status != TaskStatus.Skipped 
                && !t.IsDeleted);
        
        if (assigneeId.HasValue)
            query = query.Where(t => t.AssignedTo == assigneeId.Value);
        
        return await query.CountAsync();
    }
    
    // ========== Bulk Operations ==========
    
    public async Task<int> BulkReassignTasksAsync(List<int> taskIds, int newAssigneeId, int reassignedBy)
    {
        var newAssignee = await context.Users
            .FirstOrDefaultAsync(u => u.Id == newAssigneeId && u.Status == UserStatus.Active && !u.IsDeleted);
        
        if (newAssignee == null)
            throw new InvalidOperationException($"User with ID {newAssigneeId} not found");
        
        var tasks = await context.Tasks
            .Where(t => taskIds.Contains(t.Id) && t.Status != TaskStatus.Completed && !t.IsDeleted)
            .ToListAsync();
        
        foreach (var task in tasks)
        {
            task.AssignedTo = newAssigneeId;
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = reassignedBy;
        }
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Bulk reassigned {TaskCount} tasks to User {NewAssigneeId} by User {ReassignedBy}", 
            tasks.Count, newAssigneeId, reassignedBy);
        
        return tasks.Count;
    }
    
    public async Task<int> BulkUpdateTaskDueDatesAsync(List<int> taskIds, DateTime newDueDate, int updatedBy)
    {
        var tasks = await context.Tasks
            .Where(t => taskIds.Contains(t.Id) && t.Status != TaskStatus.Completed && !t.IsDeleted)
            .ToListAsync();
        
        foreach (var task in tasks)
        {
            task.DueAt = newDueDate;
            task.UpdatedAt = DateTime.UtcNow;
            task.UpdatedBy = updatedBy;
        }
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Bulk updated due dates for {TaskCount} tasks to {NewDueDate} by User {UpdatedBy}", 
            tasks.Count, newDueDate, updatedBy);
        
        return tasks.Count;
    }
    
    // ========== Private Helper Methods ==========
    
    private bool IsValidStatusTransition(TaskStatus from, TaskStatus to)
    {
        return (from, to) switch
        {
            (TaskStatus.Pending, TaskStatus.InProgress) => true,
            (TaskStatus.Pending, TaskStatus.Skipped) => true,
            (TaskStatus.InProgress, TaskStatus.Completed) => true,
            (TaskStatus.InProgress, TaskStatus.Pending) => true,
            (TaskStatus.Completed, _) => false,
            (TaskStatus.Skipped, _) => false,
            (_, TaskStatus.Completed) when from == TaskStatus.InProgress => true,
            (_, TaskStatus.Skipped) when from != TaskStatus.Completed => true,
            _ => false
        };
    }
    
    private bool IsValidAssigneeForTaskType(TaskType type, UserRole role)
    {
        return type switch
        {
            TaskType.Preparation => role == UserRole.Employee || role == UserRole.Admin,
            TaskType.Design => role == UserRole.Designer || role == UserRole.Admin,
            TaskType.Delivery => role == UserRole.Driver || role == UserRole.Admin,
            _ => role == UserRole.Employee || role == UserRole.Admin
        };
    }
    
    private async Task<User?> FindAvailableAssigneeForTaskType(TaskType type, int? branchId)
    {
        var role = type switch
        {
            TaskType.Preparation => UserRole.Employee,
            TaskType.Design => UserRole.Designer,
            TaskType.Delivery => UserRole.Driver,
            _ => UserRole.Employee
        };
        
        var query = context.Users
            .Where(u => u.Role == role && u.Status == UserStatus.Active && !u.IsDeleted);
        
        if (branchId.HasValue)
            query = query.Where(u => u.BranchId == branchId.Value);
        
        // Find user with least tasks assigned
        var users = await query.ToListAsync();
        
        foreach (var user in users)
        {
            var taskCount = await context.Tasks
                .CountAsync(t => t.AssignedTo == user.Id && t.Status != TaskStatus.Completed);
            
            user.UpdatedBy = taskCount; // Temporary storage for sorting
        }
        
        return users.OrderBy(u => u.UpdatedBy).FirstOrDefault();
    }
}