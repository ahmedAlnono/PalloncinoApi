namespace Palloncino.Models.Entities;
public class SubTask : BaseEntity
{
    // Properties
    public int TaskId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public int? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? Order { get; set; } // Display order
    
    // Navigation Properties
    public virtual Task? Task { get; set; }
    public virtual User? Completer { get; set; }
    
    // Validations
    // - Title required
    // - IsCompleted can only be true if CompletedBy provided
}