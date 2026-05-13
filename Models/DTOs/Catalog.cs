using System.ComponentModel.DataAnnotations;
using Palloncino.Models.Enums;

namespace Palloncino.Models.DTOs;

public class CreateCatalogItemDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = "";

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
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsRental { get; set; }
    public string? ImageUrl { get; set; }
    public string? Sku { get; set; }
    public int? StockQuantity { get; set; }
    public ItemStatus Status { get; set; }
}