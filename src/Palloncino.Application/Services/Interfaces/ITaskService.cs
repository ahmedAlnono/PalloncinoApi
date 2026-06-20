using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;

namespace Palloncino.Services.Interfaces;

public interface ITaskService
{
    // ========== CRUD Operations ==========
    Task<Models.Entities.Task> CreateTaskAsync(Models.Entities.Task task, List<CreateSubTaskDto>? subTasks = null);
    Task<Models.Entities.Task> UpdateTaskAsync(Models.Entities.Task task);
    Task<bool> DeleteTaskAsync(int taskId);
    Task<bool> SoftDeleteTaskAsync(int taskId, int deletedBy);
    
    // ========== Queries ==========
    Task<Models.Entities.Task?> GetTaskByIdAsync(int taskId);
    Task<IEnumerable<Models.Entities.Task>> GetAllTasksAsync(TaskFilter? filter = null);
    Task<IEnumerable<Models.Entities.Task>> GetTasksByJobOrderAsync(int jobOrderId);
    Task<IEnumerable<Models.Entities.Task>> GetTasksByAssigneeAsync(int assigneeId, Models.Enums.TaskStatus? status = null);
    Task<IEnumerable<Models.Entities.Task>> GetTasksByTypeAsync(TaskType type, int? branchId = null);
    
    // ========== Status Management ==========
    Task<Models.Entities.Task> UpdateTaskStatusAsync(int taskId, Models.Enums.TaskStatus status, int updatedBy, string? skipReason = null);
    Task<Models.Entities.Task> StartTaskAsync(int taskId, int startedBy);
    Task<Models.Entities.Task> CompleteTaskAsync(int taskId, int completedBy);
    Task<Models.Entities.Task> SkipTaskAsync(int taskId, int skippedBy, string reason);
    
    // ========== Assignment Management ==========
    Task<Models.Entities.Task> AssignTaskAsync(int taskId, int assignedTo, int assignedBy);
    Task<Models.Entities.Task> ReassignTaskAsync(int taskId, int newAssigneeId, int reassignedBy, string? reason = null);
    Task<bool> CompleteTaskForOthersAsync(int taskId, int completedBy, int originalAssigneeId); // BR-12
    
    // ========== SubTask Management ==========
    Task<SubTask> AddSubTaskAsync(int taskId, string title, string? description = null, int? order = null);
    Task<SubTask> UpdateSubTaskAsync(int subTaskId, string? title = null, string? description = null);
    Task<bool> RemoveSubTaskAsync(int subTaskId);
    Task<SubTask> CompleteSubTaskAsync(int subTaskId, int completedBy);
    Task<IEnumerable<SubTask>> GetSubTasksAsync(int taskId);
    
    // ========== Checklist Management ==========
    Task<ChecklistItem> AddChecklistItemAsync(int taskId, ChecklistPhase phase, string itemName);
    Task<ChecklistItem> UpdateChecklistItemAsync(int checklistItemId, bool isChecked, int checkedBy, string? proofImageUrl = null);
    Task<bool> RemoveChecklistItemAsync(int checklistItemId);
    Task<IEnumerable<ChecklistItem>> GetChecklistItemsAsync(int taskId, ChecklistPhase? phase = null);
    Task<bool> CompleteChecklistPhaseAsync(int taskId, ChecklistPhase phase, int completedBy);
    
    // ========== Validation ==========
    Task<bool> TaskExistsAsync(int taskId);
    Task<bool> IsTaskOverdueAsync(int taskId);
    Task<IEnumerable<Models.Entities.Task>> GetOverdueTasksAsync(int? assigneeId = null);
    
    // ========== Business Logic ==========
    Task<Models.Entities.Task> AutoAssignTaskAsync(int taskId);
    Task<bool> CanCompleteTaskAsync(int taskId, int userId);
    Task<TaskStatisticsDto> GetTaskStatisticsAsync(int taskId);
    
    // ========== Dashboard & Counters ==========
    Task<TaskDashboardDto> GetTaskDashboardAsync(int? branchId = null, int? assigneeId = null);
    Task<int> GetPendingTasksCountAsync(int? assigneeId = null);
    Task<int> GetOverdueTasksCountAsync(int? assigneeId = null);
    
    // ========== Bulk Operations ==========
    Task<int> BulkReassignTasksAsync(List<int> taskIds, int newAssigneeId, int reassignedBy);
    Task<int> BulkUpdateTaskDueDatesAsync(List<int> taskIds, DateTime newDueDate, int updatedBy);
}