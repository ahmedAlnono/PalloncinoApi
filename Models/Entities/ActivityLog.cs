namespace Palloncino.Models.Entities;
public class ActivityLog : BaseEntity
{
    // Properties
    public int UserId { get; set; }
    public string? Action { get; set; } // CREATE, UPDATE, DELETE, APPROVE, REJECT, SKIP, ASSIGN, COMPLETE
    public string? EntityType { get; set; } // Order, JobOrder, Task, User, etc.
    public int EntityId { get; set; }
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Notes { get; set; }
    
    // Navigation Properties
    public virtual User? User { get; set; }
    
    // Validations
    // - Action cannot be empty
    // - EntityType and EntityId required
    // - Automatically set CreatedAt = DateTime.UtcNow
}