using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;
public class User : BaseEntity
{
    // Properties
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public UserRole Role { get; set; } // Enum: Customer, Admin, Employee, Driver, Designer
    public string? ProfileImageUrl { get; set; }
    public int? BranchId { get; set; } // For internal staff
    public UserStatus Status { get; set; } // Active, Inactive, Suspended
    public DateTime? LastLoginAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    
    // Navigation Properties
    public virtual Branch? Branch { get; set; }
    public virtual ICollection<Order>? Orders { get; set; }
    public virtual ICollection<JobOrder>? AssignedJobOrders { get; set; }
    public virtual ICollection<Task>? AssignedTasks { get; set; }
    public virtual ICollection<ActivityLog>? ActivityLogs { get; set; }
    public virtual ICollection<ChatMessage>? ChatMessages { get; set; }
    public virtual ICollection<Notification>? Notifications { get; set; }
    
    // Validations
    // - Email must be unique
    // - Phone must be unique
    // - Role cannot be null
    // - PasswordHash must be hashed using BCrypt
    // - BranchId required for Role != Customer
}