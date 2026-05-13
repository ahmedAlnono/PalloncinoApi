using System.ComponentModel.DataAnnotations;
using Palloncino.Models.Enums;

namespace Palloncino.Models.DTOs
{
    // ========== Inventory Item DTOs ==========
    
    public class CreateInventoryItemDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = "";
        
        [Required]
        [MaxLength(50)]
        public string Sku { get; set; } = "";
        
        [Required]
        [Range(0, 999999.99)]
        public decimal PurchasePrice { get; set; }
        
        [Required]
        [Range(0, 999999.99)]
        public decimal SalePrice { get; set; }
        
        [Required]
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string Unit { get; set; } = "Piece";
        
        public int? BranchId { get; set; }
        
        public int? MinStockLevel { get; set; }
        
        [MaxLength(100)]
        public string? Category { get; set; }
    }
    
    public class UpdateInventoryItemDto
    {
        [MaxLength(200)]
        public string? Title { get; set; }
        
        [Range(0, 999999.99)]
        public decimal? PurchasePrice { get; set; }
        
        [Range(0, 999999.99)]
        public decimal? SalePrice { get; set; }
        
        [Range(0, int.MaxValue)]
        public int? MinStockLevel { get; set; }
        
        [MaxLength(100)]
        public string? Category { get; set; }
    }
    
    public class UpdateInventoryStockDto
    {
        [Required]
        [Range(-999999, 999999)]
        public int QuantityChange { get; set; }
        
        [Required]
        public MovementType MovementType { get; set; }
        
        [MaxLength(500)]
        public string? Reason { get; set; }
        
        public int? RelatedJobOrderId { get; set; }
    }
    
    public class InventoryItemDto : BaseDto
    {
        public string Title { get; set; } = "";
        public string Sku { get; set; } = "";
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; } = "";
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public int? MinStockLevel { get; set; }
        public string? Category { get; set; }
        public InventoryStatus Status { get; set; }
        public bool IsLowStock { get; set; }
    }
    
    // ========== Inventory Movement DTOs ==========
    
    public class InventoryMovementDto : BaseDto
    {
        public int InventoryItemId { get; set; }
        public string ItemName { get; set; } = "";
        public MovementType Type { get; set; }
        public int Quantity { get; set; }
        public int? RelatedJobOrderId { get; set; }
        public string? Reason { get; set; }
        public string PerformedByName { get; set; } = "";
    }
}