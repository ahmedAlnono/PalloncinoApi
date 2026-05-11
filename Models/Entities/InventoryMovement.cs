using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;
public class InventoryMovement : BaseEntity
{
    // Properties
    public int InventoryItemId { get; set; }
    public MovementType Type { get; set; } // Add, Remove, Return
    public int Quantity { get; set; }
    public int? RelatedJobOrderId { get; set; }
    public string? Reason { get; set; }
    public int PerformedBy { get; set; }
    
    // Navigation Properties
    public virtual InventoryItem? InventoryItem { get; set; }
    public virtual JobOrder? JobOrder { get; set; }
    public virtual User? Performer { get; set; }
    
    // Validations
    // - Quantity > 0
    // - Type must be valid
}