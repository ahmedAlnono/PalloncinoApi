using System.ComponentModel.DataAnnotations;

namespace Palloncino.Models.DTOs;

public class TemplateStatisticsDto
{
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int TotalQuantity { get; set; }
    public decimal OriginalTotalPrice { get; set; }
    public decimal DiscountedPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public int OrderCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}

public class TemplateUsageDto
{
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public decimal TotalRevenue { get; set; }
}

// ========== Template DTOs ==========

public class CreateTemplateDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "";

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
    public string Category { get; set; } = "";

    public List<CreateTemplateItemDto> Items { get; set; } = new();
}

public class CreateTemplateItemDto
{
    [Required]
    [Range(1,int.MaxValue)]
    public int CatalogItemId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;
}

public class TemplateDto : BaseDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal BeforeDiscount { get; set; }
    public decimal AfterDiscount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public string? ImageUrl { get; set; }
    public string Category { get; set; } = "";
    public List<TemplateItemDto> Items { get; set; } = new();
}

public class UpdateTemplateDto
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal? BeforeDiscount { get; set; }
    public decimal? AfterDiscount { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public string? ImageUrl { get; set; }
    public string? Category { get; set; } = "";
    public List<TemplateItemDto>? Items { get; set; }
}

public class TemplateItemDto
{
    public int CatalogItemId { get; set; }
    public string ItemTitle { get; set; } = "";
    public string? ItemImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class DuplicateTemplateRequest
{
    public string NewName { get; set; } = string.Empty;
}

public class AddTemplateItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; } = 1;
}