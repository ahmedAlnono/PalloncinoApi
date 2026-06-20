using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Palloncino.Models.Entities;

public class ActivityLog : BaseEntity
{
    // Properties
    public int UserId { get; set; }
    [Required]
    public ActionType Action { get; set; } // CREATE, UPDATE, DELETE, APPROVE, REJECT, SKIP, ASSIGN, COMPLETE
    [Required]
    public string? EntityType { get; set; } // Order, JobOrder, Task, User, etc.
    [Required]
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

public enum ActionType
{
    CREATE = 1,
    UPDATE = 2,
    DELETE = 3,
    APPROVE = 4,
    REJECT = 5,
    SKIP = 6,
    ASSIGN = 7,
    COMPLETE = 8
}

public enum EntityType
{
    Order = 1,
    JobOrder = 2,
    Task = 3,
    User = 4,
    etc = 5,
}