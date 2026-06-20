using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;
public class CatalogItem : BaseEntity
{
    // Properties
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; } // e.g., "Balloons", "Decorations", "Tables"
    public decimal Price { get; set; }
    public bool IsRental { get; set; } // TRUE = for rent (Catalog), FALSE = for sale
    public string? ImageUrl { get; set; }
    public string? Sku { get; set; } // Stock Keeping Unit
    public int? StockQuantity { get; set; } // Only if IsRental = false
    public ItemStatus Status { get; set; } // Available, OutOfStock, Discontinued
    
    // Navigation Properties
    public virtual ICollection<TemplateItem>? TemplateItems { get; set; }
    public virtual ICollection<OrderItem>? OrderItems { get; set; }
    
    // Validations
    // - Title required (max 200 chars)
    // - Price must be > 0
    // - Category cannot be empty
    // - SKU must be unique if provided
}