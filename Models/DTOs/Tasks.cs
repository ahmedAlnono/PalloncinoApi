using Palloncino.Models.Enums;
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