using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;

public class InventoryItem : BaseEntity
{
    // Properties
    public string? Title { get; set; }
    public string? Sku { get; set; } // Unique identifier
    public decimal PurchasePrice { get; set; } // Cost price
    public decimal SalePrice { get; set; } // Selling price
    public int Quantity { get; set; }
    public string? Unit { get; set; } // e.g., "Piece", "Meter", "Kg"
    public int? BranchId { get; set; } // Which branch owns this
    public int? MinStockLevel { get; set; } // Alert when low
    public string? Category { get; set; }
    public InventoryStatus Status { get; set; } // InStock, LowStock, OutOfStock

    // Navigation Properties
    public virtual Branch? Branch { get; set; }
    public virtual ICollection<OrderItem>? OrderItems { get; set; }
    public virtual ICollection<InventoryMovement>? InventoryMovements { get; set; }
    public virtual ICollection<JobOrderItem>? JobOrderItems { get; set; }

    // Validations
    // - Title required
    // - SKU must be unique
    // - Quantity >= 0
    // - PurchasePrice >= 0
    // - SalePrice >= 0
}