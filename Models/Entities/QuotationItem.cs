using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Palloncino.Models.Entities;

/// <summary>
/// Represents a single line item in a quotation
/// Based on sections: 8.3 (FR-QUO-02, FR-QUO-03)
/// </summary>
public class QuotationItem : BaseEntity
{
    // ========== Foreign Keys ==========
    
    [Required]
    public int QuotationId { get; set; }
    
    // Optional: Can be from catalog or custom manual entry
    public int? CatalogItemId { get; set; }
    public int? InventoryItemId { get; set; }
    
    // ========== Core Fields ==========
    
    [Required]
    [MaxLength(200)]
    public string ItemName { get; set; } = "";
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int Quantity { get; set; } = 1;
    
    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Unit price must be greater than or equal to 0")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; } = 0;
    
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice => Quantity * UnitPrice;
    
    // ========== Optional Fields ==========
    
    [Range(0, 100, ErrorMessage = "Discount percentage must be between 0 and 100")]
    [Column(TypeName = "decimal(5,2)")]
    public decimal? DiscountPercentage { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountAmount { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalPrice => CalculateFinalPrice();
    
    public bool IsRental { get; set; } = false;
    
    public string? Notes { get; set; }
    
    // ========== Display Order ==========
    
    public int DisplayOrder { get; set; } = 0;
    
    // ========== Navigation Properties ==========
    
    [ForeignKey(nameof(QuotationId))]
    public virtual Quotation Quotation { get; set; } = null!;
    
    [ForeignKey(nameof(CatalogItemId))]
    public virtual CatalogItem? CatalogItem { get; set; }
    
    [ForeignKey(nameof(InventoryItemId))]
    public virtual InventoryItem? InventoryItem { get; set; }
    
    // ========== Methods ==========
    
    /// <summary>
    /// Calculates the final price after applying discount
    /// </summary>
    private decimal CalculateFinalPrice()
    {
        decimal baseTotal = TotalPrice;
        
        if (DiscountAmount.HasValue && DiscountAmount.Value > 0)
        {
            return baseTotal - DiscountAmount.Value;
        }
        
        if (DiscountPercentage.HasValue && DiscountPercentage.Value > 0)
        {
            return baseTotal - (baseTotal * (DiscountPercentage.Value / 100));
        }
        
        return baseTotal;
    }
    
    /// <summary>
    /// Validates the quotation item before saving
    /// </summary>
    public (bool IsValid, string ErrorMessage) Validate()
    {
        // Either CatalogItem or InventoryItem or manual entry (ItemName must be provided)
        if (CatalogItemId == null && InventoryItemId == null && string.IsNullOrWhiteSpace(ItemName))
        {
            return (false, "Either select a catalog/inventory item or provide an item name");
        }
        
        if (Quantity < 1)
        {
            return (false, "Quantity must be at least 1");
        }
        
        if (UnitPrice < 0)
        {
            return (false, "Unit price cannot be negative");
        }
        
        // Validate discount
        if (DiscountPercentage.HasValue && (DiscountPercentage.Value < 0 || DiscountPercentage.Value > 100))
        {
            return (false, "Discount percentage must be between 0 and 100");
        }
        
        if (DiscountAmount.HasValue && DiscountAmount.Value < 0)
        {
            return (false, "Discount amount cannot be negative");
        }
        
        if (DiscountAmount.HasValue && DiscountAmount.Value > TotalPrice)
        {
            return (false, "Discount amount cannot exceed total price");
        }
        
        return (true, "");
    }
    
    /// <summary>
    /// Creates a copy of this QuotationItem (for versioning)
    /// </summary>
    public QuotationItem Clone()
    {
        return new QuotationItem
        {
            QuotationId = this.QuotationId,
            CatalogItemId = this.CatalogItemId,
            InventoryItemId = this.InventoryItemId,
            ItemName = this.ItemName,
            Description = this.Description,
            Quantity = this.Quantity,
            UnitPrice = this.UnitPrice,
            DiscountPercentage = this.DiscountPercentage,
            DiscountAmount = this.DiscountAmount,
            IsRental = this.IsRental,
            Notes = this.Notes,
            DisplayOrder = this.DisplayOrder
        };
    }
}