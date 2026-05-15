namespace Palloncino.Models.Entities;
public class Template : BaseEntity
{
    // Properties
    public string? Title { get; set; } // e.g., "Graduation Party", "Birthday Bash"
    public string? Description { get; set; }
    public decimal BeforeDiscount { get; set; } // Original price
    public decimal AfterDiscount { get; set; } // Discounted price
    public string? ImageUrl { get; set; }
    public string? Category { get; set; } // e.g., "Graduation", "Birthday", "Engagement"    
    // Navigation Properties
    public virtual ICollection<TemplateItem> TemplateItems { get; set; } = [];
    public virtual ICollection<Order>? Orders { get; set; }
    
    // Validations
    // - Title required
    // - AfterDiscount must be <= BeforeDiscount
    // - AfterDiscount > 0
}