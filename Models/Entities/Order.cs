using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;
public class Order : BaseEntity
{
    // Properties
    public int CustomerId { get; set; }
    public OrderType Type { get; set; } // Regular, Custom, Design
    public OrderSource Source { get; set; } // MobileApp, Website, WalkIn
    public OrderStatus Status { get; set; } // PendingReview, Approved, Rejected, Converted
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public string? CustomDesignDescription { get; set; } // For custom orders
    public DateTime? RequiredDate { get; set; }
    public string? RejectionReason { get; set; }
    public string? Address { get; set; }
    public decimal? DeliveryFee { get; set; }
    
    // Navigation Properties
    public virtual User? Customer { get; set; }
    public virtual ICollection<OrderItem>? OrderItems { get; set; }
    public virtual ICollection<Attachment>? Attachments { get; set; }
    public virtual ICollection<Quotation>? Quotations { get; set; }
    public virtual JobOrder? JobOrder { get; set; }
    
    // Validations
    // - TotalAmount >= 0
    // - If Type == Custom or Design, CustomDesignDescription required
    // - CustomerId must exist
}