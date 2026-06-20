using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;

public class JobOrder : BaseEntity
{
    // Properties
    public int? SourceOrderId { get; set; } // If created from customer order
    public string JobNumber { get; set; } = string.Empty; // Auto-generated: JO-2024-0001
    public ExecutionType ExecutionType { get; set; } // PickupFromBranch, DeliveryOnly, DeliveryWithInstallation
    public JobOrderStatus Status { get; set; } // Pending, InProgress, ReadyForDelivery, Delivered, WaitingReturn, Completed, Cancelled
    public DateTime DueAt { get; set; } // Delivery deadline
    public int BranchId { get; set; }
    public int? AssignedToCoordinator { get; set; }
    public string? SpecialInstructions { get; set; }
    public string? DeliveryAddress { get; set; }
    public DateTime? ActualDeliveryAt { get; set; }
    public DateTime? ActualReturnAt { get; set; }
    public decimal TotalCost { get; set; } // Calculated from inventory usage
    public decimal TotalRevenue { get; set; } // From order
    public decimal Profit => TotalRevenue - TotalCost;

    // Navigation Properties
    public virtual Order? SourceOrder { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual User? Coordinator { get; set; }
    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
    public virtual ICollection<JobOrderItem> JobOrderItems { get; set; } = new List<JobOrderItem>();

    // Validations
    // - DueAt must be in future
    // - JobNumber unique
    // - Status transitions must follow business rules
}
