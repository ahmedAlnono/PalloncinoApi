namespace Palloncino.Models.Entities;
public class OrderItem : BaseEntity
{
    // Properties
    public int OrderId { get; set; }
    public int? CatalogItemId { get; set; } // From store
    public int? InventoryItemId { get; set; } // From inventory
    public string? ItemName { get; set; } // Snapshot name
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public bool IsRental { get; set; }
    
    // Navigation Properties
    public virtual Order? Order { get; set; }
    public virtual CatalogItem? CatalogItem { get; set; }
    public virtual InventoryItem? InventoryItem { get; set; }
    
    // Validations
    // - Quantity >= 1
    // - UnitPrice >= 0
    // - TotalPrice = Quantity * UnitPrice
    // - Either CatalogItemId OR InventoryItemId must be provided
}