using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;
public class ChecklistItem : BaseEntity
{
    // Properties
    public int TaskId { get; set; }
    public ChecklistPhase Phase { get; set; } // LoadingFromBranch, DeliveryToCustomer, ReturnRental
    public string? ItemName { get; set; }
    public bool IsChecked { get; set; }
    public int? CheckedBy { get; set; }
    public DateTime? CheckedAt { get; set; }
    public string? ProofImageUrl { get; set; } // Photo evidence
    
    // Navigation Properties
    public virtual Task? Task { get; set; }
    public virtual User? Checker { get; set; }
    
    // Validations
    // - ItemName required
    // - ProofImageUrl required if Phase == DeliveryToCustomer
}