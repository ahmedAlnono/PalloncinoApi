using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;
public class Notification : BaseEntity
{
    // Properties
    public int RecipientId { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public NotificationType Type { get; set; } // OrderUpdate, TaskAssigned, Alert, Promotional
    public string? ImageUrl { get; set; }
    public int? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? SentAt { get; set; }
    
    // Navigation Properties
    public virtual User? Recipient { get; set; }
    
    // Validations
    // - Title and Body required
    // - SentAt set automatically
}