using Palloncino.Models.Entities;
namespace Palloncino.Models.DTOs;
public class UpdateQuotationItemDto
{
    public int Id { get; set; }
    public string? ItemName { get; set; }
    public string? Description { get; set; }
    public int? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public decimal? DiscountAmount { get; set; }
    public bool? IsRental { get; set; }
    public string? Notes { get; set; }
}

public class QuotationPdfDto
{
    public Quotation Quotation { get; set; } = null!;
    public Order Order { get; set; } = null!;
    public User Customer { get; set; } = null!;
    public CompanyInfoDto CompanyInfo { get; set; } = new();
}

public class CompanyInfoDto
{
    public string Name { get; set; } = "Palloncino";
    public string LogoUrl { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
}