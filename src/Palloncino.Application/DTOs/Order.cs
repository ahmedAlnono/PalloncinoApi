using System.ComponentModel.DataAnnotations;
using Palloncino.Models.Enums;

namespace Palloncino.Models.DTOs;

// ========== Order DTOs ==========

public class CreateOrderDto
{
    [Required]
    public OrderType Type { get; set; } = OrderType.Regular;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(2000)]
    public string? CustomDesignDescription { get; set; }

    public DateTime? RequiredDate { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public decimal? DeliveryFee { get; set; }

    public List<CreateOrderItemDto> Items { get; set; } = new();

    public List<IFormFile>? Attachments { get; set; }
}

public class CreateOrderItemDto
{
    public int? CatalogItemId { get; set; }
    public int? InventoryItemId { get; set; }
    public string? ItemName { get; set; } // For custom items
    public int Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
    public bool IsRental { get; set; } = false;
}

public class UpdateOrderStatusDto
{
    [Required]
    public OrderStatus Status { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }
}

public class OrderDto : BaseDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public OrderType Type { get; set; }
    public OrderSource Source { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public string? CustomDesignDescription { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? RejectionReason { get; set; }
    public string? Address { get; set; }
    public decimal? DeliveryFee { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public List<AttachmentDto> Attachments { get; set; } = new();
    public int? JobOrderId { get; set; }
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int? CatalogItemId { get; set; }
    public string ItemName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public bool IsRental { get; set; }
}

// ========== Quotation DTOs ==========

public class CreateQuotationDto
{
    [Required]
    public int OrderId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime? ValidUntil { get; set; }

    public List<CreateQuotationItemDto> Items { get; set; } = new();
}

public class CreateQuotationItemDto
{
    [MaxLength(200)]
    public string ItemName { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    [Required]
    [Range(0, 999999.99)]
    public decimal UnitPrice { get; set; }

    [Range(0, 100)]
    public decimal? DiscountPercentage { get; set; }

    public decimal? DiscountAmount { get; set; }

    public bool IsRental { get; set; } = false;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int? CatalogItemId { get; set; }
    public int? InventoryItemId { get; set; }
}

public class QuotationDto : BaseDto
{
    public int OrderId { get; set; }
    public string QuotationNumber { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public QuotationStatus Status { get; set; }
    public DateTime ValidUntil { get; set; }
    public List<QuotationItemDto> Items { get; set; } = new();
}

public class QuotationItemDto
{
    public int Id { get; set; }
    public string ItemName { get; set; } = "";
    public string? Description { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal FinalPrice { get; set; }
    public bool IsRental { get; set; }
    public string? Notes { get; set; }
}

public class QuotationPrintDto
{
    public string CompanyName { get; set; } = "Palloncino";
    public string CompanyLogo { get; set; } = "";
    public string CompanyAddress { get; set; } = "";
    public string CompanyPhone { get; set; } = "";
    public string CompanyEmail { get; set; } = "";

    public QuotationDto Quotation { get; set; } = null!;
    public OrderDto Order { get; set; } = null!;
}
public class OrderStatisticsDto
{
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int ApprovedOrders { get; set; }
    public int RejectedOrders { get; set; }
    public int ConvertedOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
}