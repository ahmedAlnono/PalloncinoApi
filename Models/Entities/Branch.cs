using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;
public class Branch : BaseEntity
{
    // Properties
    public string? Name { get; set; } // e.g., "Al-Bustan", "Al-Rawda"
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? ManagerName { get; set; }
    public BranchStatus Status { get; set; } // Active, Inactive
    
    // Navigation Properties
    public virtual ICollection<User>? Users { get; set; }
    public virtual ICollection<JobOrder>? JobOrders { get; set; }
    public virtual ICollection<InventoryItem>? InventoryItems { get; set; }
    
    // Validations
    // - Name is required (max 100 chars)
    // - Address is required
}