using System.ComponentModel.DataAnnotations;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using TaskStatus = Palloncino.Models.Enums.TaskStatus;

namespace Palloncino.Models.DTOs;

// ========== Job Order DTOs ==========

public class CreateJobOrderDto
{
    public int? SourceOrderId { get; set; }
    
    [Required]
    public ExecutionType ExecutionType { get; set; }
    
    [Required]
    public DateTime DueAt { get; set; }
    
    [Required]
    public int BranchId { get; set; }
    
    public int? AssignedToCoordinator { get; set; }
    
    [MaxLength(2000)]
    public string? SpecialInstructions { get; set; }
    
    [MaxLength(500)]
    public string? DeliveryAddress { get; set; }
    
    public List<int>? TaskIds { get; set; } // For manual tasks
}

public class UpdateJobOrderDto
{
    public ExecutionType? ExecutionType { get; set; }
    
    public DateTime? DueAt { get; set; }
    
    public int? AssignedToCoordinator { get; set; }
    
    [MaxLength(2000)]
    public string? SpecialInstructions { get; set; }
    
    [MaxLength(500)]
    public string? DeliveryAddress { get; set; }
    
    public JobOrderStatus? Status { get; set; }
}

public class UpdateJobOrderStatusDto
{
    [Required]
    public JobOrderStatus Status { get; set; }
    
    [MaxLength(500)]
    public string? Reason { get; set; }
}

public class SkipReturnDto
{
    [Required]
    public int JobOrderId { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = "";
}

public class JobOrderDto : BaseDto
{
    public string JobNumber { get; set; } = "";
    public int? SourceOrderId { get; set; }
    public string? OrderNumber { get; set; }
    public ExecutionType ExecutionType { get; set; }
    public JobOrderStatus Status { get; set; }
    public DateTime DueAt { get; set; }
    public double CountdownSeconds { get; set; } // BR-09
    public string CountdownDisplay { get; set; } = "";
    public int BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public int? AssignedToCoordinator { get; set; }
    public string? CoordinatorName { get; set; }
    public string? SpecialInstructions { get; set; }
    public string? DeliveryAddress { get; set; }
    public DateTime? ActualDeliveryAt { get; set; }
    public DateTime? ActualReturnAt { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal Profit { get; set; }
    public List<TaskDto> Tasks { get; set; } = new();
    public List<JobOrderItemDto> JobOrderItems { get; set; } = new();
}

public class JobOrderListDto : BaseDto
{
    public string JobNumber { get; set; } = "";
    public ExecutionType ExecutionType { get; set; }
    public JobOrderStatus Status { get; set; }
    public DateTime DueAt { get; set; }
    public double CountdownSeconds { get; set; }
    public string CountdownDisplay { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string? CoordinatorName { get; set; }
    public int TaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
}

// ========== Job Order Item DTOs ==========

public class AddJobOrderItemDto
{
    [Required]
    public int InventoryItemId { get; set; }
    
    [Required]
    [Range(1, int.MaxValue)]
    public int QuantityUsed { get; set; } = 1;
    
    public decimal? SellingPricePerUnit { get; set; }
    
    public bool IsRental { get; set; } = false;
    
    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateJobOrderItemDto
{
    [Range(1, int.MaxValue)]
    public int? QuantityUsed { get; set; }
    
    public decimal? SellingPricePerUnit { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class ReturnRentalItemDto
{
    [Required]
    public int JobOrderItemId { get; set; }
    
    [Required]
    public ReturnCondition Condition { get; set; }
    
    public decimal? DamageDeduction { get; set; }
    
    [MaxLength(500)]
    public string? DeductionReason { get; set; }
    
    public IFormFile? ProofImage { get; set; }
}

public class JobOrderItemDto : BaseDto
{
    public int JobOrderId { get; set; }
    public int InventoryItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string? Sku { get; set; }
    public int QuantityUsed { get; set; }
    public string Unit { get; set; } = "";
    public decimal CostPerUnit { get; set; }
    public decimal TotalCost { get; set; }
    public decimal? SellingPricePerUnit { get; set; }
    public decimal? TotalSellingPrice { get; set; }
    public bool IsRental { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public ReturnCondition? ReturnCondition { get; set; }
    public decimal? DamageDeduction { get; set; }
    public string? DeductionReason { get; set; }
    public JobOrderItemStatus Status { get; set; }
    public ItemPhase Phase { get; set; }
    public string? ProofImageUrl { get; set; }
    public DateTime? PreparedAt { get; set; }
    public string? PreparedByName { get; set; }
}

// ========== Task DTOs ==========

public class CreateTaskDto
{
    [Required]
    public int JobOrderId { get; set; }
    
    [Required]
    public TaskType Type { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "";
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public int? AssignedTo { get; set; }
    
    public DateTime? DueAt { get; set; }
    
    public List<CreateSubTaskDto>? SubTasks { get; set; }
}

public class CreateSubTaskDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "";
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public int? Order { get; set; }
}

public class UpdateTaskDto
{
    [MaxLength(200)]
    public string? Title { get; set; }
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public int? AssignedTo { get; set; }
    
    public DateTime? DueAt { get; set; }
}

public class UpdateTaskStatusDto
{
    [Required]
    public TaskStatus Status { get; set; }
    
    [MaxLength(500)]
    public string? SkipReason { get; set; } // Required if Status == Skipped
}

public class CompleteSubTaskDto
{
    [Required]
    public int SubTaskId { get; set; }
    
    public bool IsCompleted { get; set; } = true;
}

public class TaskDto : BaseDto
{
    public int JobOrderId { get; set; }
    public string JobNumber { get; set; } = "";
    public TaskType Type { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public TaskStatus Status { get; set; }
    public DateTime DueAt { get; set; }
    public double CountdownSeconds { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedBy { get; set; }
    public string? CompletedByName { get; set; }
    public string? SkipReason { get; set; }
    public List<SubTaskDto> SubTasks { get; set; } = new();
    public List<ChecklistItemDto> ChecklistItems { get; set; } = new();
}

public class SubTaskDto : BaseDto
{
    public int TaskId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public int? CompletedBy { get; set; }
    public string? CompletedByName { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? Order { get; set; }
}

// ========== Checklist DTOs ==========

public class UpdateChecklistItemDto
{
    [Required]
    public int ChecklistItemId { get; set; }
    
    [Required]
    public bool IsChecked { get; set; }
    
    public IFormFile? ProofImage { get; set; }
}

public class ChecklistItemDto
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public ChecklistPhase Phase { get; set; }
    public string ItemName { get; set; } = "";
    public bool IsChecked { get; set; }
    public int? CheckedBy { get; set; }
    public string? CheckedByName { get; set; }
    public DateTime? CheckedAt { get; set; }
    public string? ProofImageUrl { get; set; }
}

public class DriverDeliveryListDto
{
    public int JobOrderId { get; set; }
    public string JobNumber { get; set; } = "";
    public string DeliveryAddress { get; set; } = "";
    public DateTime DueAt { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public ExecutionType ExecutionType { get; set; }
    public JobOrderStatus Status { get; set; }
    public List<string> ItemsSummary { get; set; } = new();
}

public class JobOrderFilter
{
public JobOrderStatus? Status { get; set; }
public ExecutionType? ExecutionType { get; set; }
public int? BranchId { get; set; }
public int? AssignedToCoordinator { get; set; }
public DateTime? FromDate { get; set; }
public DateTime? ToDate { get; set; }
public bool IncludeCompleted { get; set; } = true;
}

public class JobOrderDashboardDto
{
public int TotalActiveOrders { get; set; }
public int ReadyForDelivery { get; set; }
public int OutForDelivery { get; set; }
public int TodayDeliveries { get; set; }
public int OverdueOrders { get; set; }
public int WaitingReturn { get; set; }
public int PendingTasks { get; set; }
public int InProgressTasks { get; set; }
public List<UpcomingDeliveryDto> UpcomingDeliveries { get; set; } = new();
}

public class UpcomingDeliveryDto
{
public int JobOrderId { get; set; }
public string JobNumber { get; set; } = string.Empty;
public DateTime DueAt { get; set; }
public string CustomerName { get; set; } = string.Empty;
public string DeliveryAddress { get; set; } = string.Empty;
public int ItemsCount { get; set; }
}

public class DeliveryChecklistDto
{
public int JobOrderId { get; set; }
public string JobNumber { get; set; } = string.Empty;
public string DeliveryAddress { get; set; } = string.Empty;
public string CustomerName { get; set; } = string.Empty;
public string CustomerPhone { get; set; } = string.Empty;
public List<ChecklistPhaseDto> LoadingItems { get; set; } = new();
public List<ChecklistPhaseDto> DeliveryItems { get; set; } = new();
public List<ChecklistPhaseDto> ReturnItems { get; set; } = new();
}

public class ChecklistPhaseDto
{
public int JobOrderItemId { get; set; }
public string ItemName { get; set; } = string.Empty;
public int Quantity { get; set; }
public bool IsChecked { get; set; }
public string? ProofImageUrl { get; set; }
}

public class ChecklistUpdateDto
{
public int JobOrderItemId { get; set; }
public bool IsChecked { get; set; }
public string? ProofImageUrl { get; set; }
}

