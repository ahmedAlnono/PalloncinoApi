namespace Palloncino.Models.Entities;
public class TemplateItem : BaseEntity
{
    // Properties
    public int TemplateId { get; set; }
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; } = 1;
    
    // Navigation Properties
    public virtual Template? Template { get; set; }
    public virtual CatalogItem? CatalogItem { get; set; }
    
    // Validations
    // - Quantity must be >= 1
    // - Unique constraint: (TemplateId, CatalogItemId)
}