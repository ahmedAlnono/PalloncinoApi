using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;
public class Task : BaseEntity
{
    // Properties
    public int JobOrderId { get; set; }
    public TaskType Type { get; set; } // Preparation, Design, Delivery
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? AssignedTo { get; set; }
    public Enums.TaskStatus Status { get; set; } // Pending, InProgress, Completed, Skipped, Overdue
    public DateTime DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedBy { get; set; } // Can be different from AssignedTo (BR-12)
    public string? SkipReason { get; set; }
    
    // Navigation Properties
    public virtual JobOrder? JobOrder { get; set; }
    public virtual User? Assignee { get; set; }
    public virtual User? Completer { get; set; }
    public virtual ICollection<SubTask> SubTasks { get; set; } = new List<SubTask>();
    public virtual ICollection<ChecklistItem> ChecklistItems { get; set; } = new List<ChecklistItem>();
    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    
    // Validations
    // - DueAt based on JobOrder.DueAt
    // - If Type == Design, must have AssignedTo from Designers role
    // - SkipReason required if Status == Skipped
}
