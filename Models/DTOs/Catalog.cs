using System.ComponentModel.DataAnnotations;
using Palloncino.Models.Enums;

namespace Palloncino.Models.DTOs
{
    // ========== Catalog Item DTOs ==========
    
    public class CreateCatalogItemDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;
        
        [Required]
        [Range(0.01, 999999.99)]
        public decimal Price { get; set; }
        
        public bool IsRental { get; set; } = false;
        
        public string? ImageUrl { get; set; }
        
        [MaxLength(50)]
        public string? Sku { get; set; }
        
        [Range(0, int.MaxValue)]
        public int? StockQuantity { get; set; }
        
        public ItemStatus Status { get; set; } = ItemStatus.Available;
    }
    
    public class UpdateCatalogItemDto
    {
        [MaxLength(200)]
        public string? Title { get; set; }
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [MaxLength(100)]
        public string? Category { get; set; }
        
        [Range(0.01, 999999.99)]
        public decimal? Price { get; set; }
        
        public bool? IsRental { get; set; }
        
        public string? ImageUrl { get; set; }
        
        [Range(0, int.MaxValue)]
        public int? StockQuantity { get; set; }
        
        public ItemStatus? Status { get; set; }
    }
    
    public class CatalogItemDto : BaseDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsRental { get; set; }
        public string? ImageUrl { get; set; }
        public string? Sku { get; set; }
        public int? StockQuantity { get; set; }
        public ItemStatus Status { get; set; }
    }
    
    // ========== Template DTOs ==========
    
    public class CreateTemplateDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [Required]
        [Range(0.01, 999999.99)]
        public decimal BeforeDiscount { get; set; }
        
        [Required]
        [Range(0.01, 999999.99)]
        public decimal AfterDiscount { get; set; }
        
        public string? ImageUrl { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;
        
        public List<CreateTemplateItemDto> Items { get; set; } = new();
    }
    
    public class CreateTemplateItemDto
    {
        [Required]
        public int CatalogItemId { get; set; }
        
        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;
    }
    
    public class TemplateDto : BaseDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal BeforeDiscount { get; set; }
        public decimal AfterDiscount { get; set; }
        public decimal DiscountPercentage { get; set; }
        public string? ImageUrl { get; set; }
        public string Category { get; set; } = string.Empty;
        public List<TemplateItemDto> Items { get; set; } = new();
    }
    
    public class TemplateItemDto
    {
        public int CatalogItemId { get; set; }
        public string ItemTitle { get; set; } = string.Empty;
        public string? ItemImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}