using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;
public class Quotation : BaseEntity
{
    // Properties
    public int OrderId { get; set; }
    public string? QuotationNumber { get; set; } // Auto-generated: Q-2024-0001
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public string? PrintableVersion { get; set; } // HTML or PDF path
    public QuotationStatus Status { get; set; } // Draft, Sent, Approved, Expired
    public DateTime ValidUntil { get; set; }
    
    // Navigation Properties
    public virtual Order? Order { get; set; }
    public virtual ICollection<QuotationItem> QuotationItems { get; set; } = [];
    
    // Validations
    // - TotalAmount > 0
    // - ValidUntil > CreatedAt
    // - QuotationNumber unique
}