using Palloncino.Models.Enums;
using TaskStatus = Palloncino.Models.Enums.TaskStatus;
namespace Palloncino.Models.DTOs;

public class TaskFilter
{
    public TaskType? Type { get; set; }
    public Models.Enums.TaskStatus? Status { get; set; }
    public int? JobOrderId { get; set; }
    public int? AssigneeId { get; set; }
    public int? BranchId { get; set; }
    public DateTime? FromDueDate { get; set; }
    public DateTime? ToDueDate { get; set; }
    public bool IncludeCompleted { get; set; } = false;
}

public class TaskStatisticsDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public TaskType Type { get; set; }
    public int TotalSubTasks { get; set; }
    public int CompletedSubTasks { get; set; }
    public int TotalChecklistItems { get; set; }
    public int CompletedChecklistItems { get; set; }
    public double CompletionPercentage { get; set; }
    public TimeSpan? TimeSpent { get; set; }
    public bool IsOverdue { get; set; }
    public string? AssignedToName { get; set; }
    public string? CompletedByName { get; set; }
}

public class TaskDashboardDto
{
    public int TotalTasks { get; set; }
    public int PendingTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int SkippedTasks { get; set; }
    public int CompletedByOthers { get; set; } // BR-12 tracking
    public List<UpcomingTaskDto> UpcomingDeadlines { get; set; } = new();
}

public class UpcomingTaskDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string JobNumber { get; set; } = string.Empty;
    public DateTime DueAt { get; set; }
    public TaskType Type { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
}

// ========== Design Task DTOs ==========

public class DesignTaskDetailsDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string? TaskDescription { get; set; }
    public Enums.TaskStatus TaskStatus { get; set; }
    public DateTime DueAt { get; set; }
    public int? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public int JobOrderId { get; set; }
    public string? JobNumber { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public string? DesignBrief { get; set; }
    public string? SpecialInstructions { get; set; }
    public List<AttachmentDto> ReferenceImages { get; set; } = new();
    public List<AttachmentDto> DesignProposals { get; set; } = new();
    public List<StatusHistoryDto> StatusHistory { get; set; } = new();
}

public class StatusHistoryDto
{
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public int ChangedBy { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Notes { get; set; }
}

public class UploadDesignProposalRequest
{
    public List<IFormFile> Files { get; set; } = new();
    public string? Description { get; set; }
}

public class UpdateDesignStatusRequest
{
    public string Status { get; set; } = string.Empty; // in_progress, pending_review, completed
    public string? Notes { get; set; }
}

public class PendingDesignTaskDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public int JobOrderId { get; set; }
    public string? JobNumber { get; set; }
    public string? CustomerName { get; set; }
    public DateTime DueAt { get; set; }
    public int DaysRemaining { get; set; }
    public bool HasProposals { get; set; }
}

public class DesignReviewTaskDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public int JobOrderId { get; set; }
    public string? JobNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? DesignerName { get; set; }
    public DateTime DueAt { get; set; }
    public int ProposalCount { get; set; }
    public DateTime? LatestProposalAt { get; set; }
}

public class AddDesignFeedbackRequest
{
    public string Feedback { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}


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