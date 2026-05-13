using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Palloncino.Models.Entities
{
    /// <summary>
    /// Audit trail for JobOrderItem changes
    /// Required for FR-SEC-02 and FR-SEC-03
    /// </summary>
    public class JobOrderItemHistory : BaseEntity
    {
        [Required]
        public int JobOrderItemId { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = ""; // ADDED, UPDATED, RETURNED, DAMAGED, LOST
        
        [Required]
        public int PerformedBy { get; set; }
        
        [Required]
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
        
        public string? OldValues { get; set; } // JSON
        
        public string? NewValues { get; set; } // JSON
        
        [MaxLength(500)]
        public string? Reason { get; set; }
        
        // Navigation
        [ForeignKey(nameof(JobOrderItemId))]
        public virtual JobOrderItem JobOrderItem { get; set; } = null!;
        
        [ForeignKey(nameof(PerformedBy))]
        public virtual User Performer { get; set; } = null!;
    }
}
