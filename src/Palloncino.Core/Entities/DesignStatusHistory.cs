namespace Palloncino.Models.Entities;

public class DesignStatusHistory : BaseEntity
{
    public int TaskId { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public int ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Notes { get; set; }
    
    // Navigation
    public virtual Models.Entities.Task Task { get; set; } = null!;
    public virtual User ChangedByUser { get; set; } = null!;
}