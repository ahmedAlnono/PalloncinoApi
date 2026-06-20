using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Palloncino.Models.Entities
{
    /// <summary>
    /// Represents actual items used/consumed in executing a Job Order
    /// Based on SRS sections: 8.4 (Job Order Module), 8.5 (Task Management)
    /// Specifically addresses: FR-JOB-09, FR-TASK-08, FR-TASK-09
    /// </summary>
    public class JobOrderItem : BaseEntity
    {
        // ========== Foreign Keys ==========
        
        /// <summary>
        /// Reference to the Job Order this item belongs to
        /// </summary>
        [Required]
        public int JobOrderId { get; set; }
        
        /// <summary>
        /// Reference to original Order Item (if this item came from customer order)
        /// Optional - null if added manually during execution
        /// </summary>
        public int? SourceOrderItemId { get; set; }
        
        /// <summary>
        /// Reference to Inventory Item (actual item from stock)
        /// Required - all job order items must come from inventory
        /// </summary>
        [Required]
        public int InventoryItemId { get; set; }
        
        // ========== Core Fields ==========
        
        /// <summary>
        /// Snapshot of item name at time of execution
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string ItemName { get; set; } = "";
        
        /// <summary>
        /// SKU snapshot for traceability
        /// </summary>
        [MaxLength(50)]
        public string? Sku { get; set; }
        
        /// <summary>
        /// Quantity used/prepared for this job
        /// </summary>
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int QuantityUsed { get; set; } = 1;
        
        /// <summary>
        /// Unit of measurement (e.g., "Piece", "Meter", "Kg")
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Unit { get; set; } = "Piece";
        
        // ========== Pricing Fields ==========
        
        /// <summary>
        /// Cost price per unit (from inventory at time of use)
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostPerUnit { get; set; }
        
        /// <summary>
        /// Total cost = QuantityUsed * CostPerUnit
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost => QuantityUsed * CostPerUnit;
        
        /// <summary>
        /// Selling price per unit (could be from order or manual)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? SellingPricePerUnit { get; set; }
        
        /// <summary>
        /// Total selling price
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalSellingPrice => SellingPricePerUnit.HasValue 
            ? QuantityUsed * SellingPricePerUnit.Value 
            : null;
        
        // ========== Rental-Specific Fields ==========
        
        /// <summary>
        /// Is this a rental item?
        /// </summary>
        public bool IsRental { get; set; } = false;
        
        /// <summary>
        /// For rental items: when was it returned?
        /// </summary>
        public DateTime? ReturnedAt { get; set; }
        
        /// <summary>
        /// For rental items: who received the return?
        /// </summary>
        public int? ReturnedToId { get; set; }
        
        /// <summary>
        /// For rental items: condition upon return
        /// </summary>
        public ReturnCondition? ReturnCondition { get; set; }
        
        /// <summary>
        /// For damaged/lost rental items: deduction amount
        /// Based on FR-JOB-09
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? DamageDeduction { get; set; }
        
        /// <summary>
        /// Reason for deduction (e.g., "Lost balloon", "Damaged stand")
        /// </summary>
        [MaxLength(500)]
        public string? DeductionReason { get; set; }
        
        // ========== Item Status Fields ==========
        
        /// <summary>
        /// Current status of this item within the job order
        /// </summary>
        public JobOrderItemStatus Status { get; set; } = JobOrderItemStatus.Pending;
        
        /// <summary>
        /// Which phase this item belongs to (for checklist grouping)
        /// </summary>
        public ItemPhase Phase { get; set; } = ItemPhase.Preparation;
        
        // ========== Tracking Fields ==========
        
        /// <summary>
        /// Who added this item to the job order
        /// </summary>
        [Required]
        public int AddedBy { get; set; }
        
        /// <summary>
        /// When was this item added
        /// </summary>
        [Required]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When was this item marked as ready/prepared
        /// </summary>
        public DateTime? PreparedAt { get; set; }
        
        /// <summary>
        /// Who prepared this item
        /// </summary>
        public int? PreparedBy { get; set; }
        
        /// <summary>
        /// Notes or special instructions for this specific item
        /// </summary>
        [MaxLength(500)]
        public string? Notes { get; set; }
        
        // ========== Checklist Integration ==========
        
        /// <summary>
        /// Reference to checklist item IDs that include this item
        /// Stored as JSON array or comma-separated
        /// </summary>
        [MaxLength(500)]
        public string? ChecklistItemIds { get; set; }
        
        // ========== Proof/Documentation ==========
        
        /// <summary>
        /// Photo proof of item preparation/delivery/return
        /// </summary>
        [MaxLength(500)]
        public string? ProofImageUrl { get; set; }
        
        // ========== Navigation Properties ==========
        
        [ForeignKey(nameof(JobOrderId))]
        public virtual JobOrder JobOrder { get; set; } = null!;
        
        [ForeignKey(nameof(InventoryItemId))]
        public virtual InventoryItem InventoryItem { get; set; } = null!;
        
        [ForeignKey(nameof(AddedBy))]
        public virtual User AddedByUser { get; set; } = null!;
        
        [ForeignKey(nameof(PreparedBy))]
        public virtual User? PreparedByUser { get; set; }
        
        [ForeignKey(nameof(ReturnedToId))]
        public virtual User? ReturnedToUser { get; set; }
        
        // ========== Methods ==========
        
        /// <summary>
        /// Mark this item as prepared
        /// </summary>
        public void MarkAsPrepared(int userId)
        {
            if (Status == JobOrderItemStatus.Pending)
            {
                Status = JobOrderItemStatus.Prepared;
                PreparedAt = DateTime.UtcNow;
                PreparedBy = userId;
                UpdateTimestamp(userId);
            }
        }
        
        /// <summary>
        /// Mark this item as delivered
        /// </summary>
        public void MarkAsDelivered(int userId)
        {
            if (Status == JobOrderItemStatus.Prepared || Status == JobOrderItemStatus.Pending)
            {
                Status = JobOrderItemStatus.Delivered;
                Phase = ItemPhase.Delivery;
                UpdateTimestamp(userId);
            }
        }
        
        /// <summary>
        /// Mark rental item as returned
        /// </summary>
        public void MarkAsReturned(int userId, ReturnCondition condition, string? proofImageUrl = null)
        {
            if (!IsRental)
                throw new InvalidOperationException("Cannot return a non-rental item");
            
            if (Status != JobOrderItemStatus.Delivered)
                throw new InvalidOperationException("Item must be delivered before return");
            
            Status = JobOrderItemStatus.Returned;
            ReturnedAt = DateTime.UtcNow;
            ReturnedToId = userId;
            ReturnCondition = condition;
            
            if (!string.IsNullOrEmpty(proofImageUrl))
                ProofImageUrl = proofImageUrl;
            
            UpdateTimestamp(userId);
        }
        
        /// <summary>
        /// Apply damage deduction for rental item
        /// Based on FR-JOB-09
        /// </summary>
        public void ApplyDamageDeduction(decimal amount, string reason, int userId)
        {
            if (!IsRental)
                throw new InvalidOperationException("Cannot apply deduction to non-rental item");
            
            if (amount < 0)
                throw new ArgumentException("Deduction amount cannot be negative");
            
            DamageDeduction = amount;
            DeductionReason = reason;
            Status = JobOrderItemStatus.DamagedReturned;
            UpdateTimestamp(userId);
        }
        
        /// <summary>
        /// Mark item as lost (not returned)
        /// </summary>
        public void MarkAsLost(int userId)
        {
            if (!IsRental)
                throw new InvalidOperationException("Cannot mark non-rental item as lost");
            
            Status = JobOrderItemStatus.Lost;
            UpdateTimestamp(userId);
        }
        
        /// <summary>
        /// Calculate profit for this specific item
        /// </summary>
        public decimal CalculateProfit()
        {
            if (!SellingPricePerUnit.HasValue)
                return -TotalCost; // No selling price, pure cost
            
            return (TotalSellingPrice ?? 0) - TotalCost - (DamageDeduction ?? 0);
        }
        
        /// <summary>
        /// Validate before saving
        /// </summary>
        public (bool IsValid, string ErrorMessage) Validate()
        {
            if (QuantityUsed <= 0)
                return (false, "Quantity must be greater than 0");
            
            if (CostPerUnit < 0)
                return (false, "Cost per unit cannot be negative");
            
            if (IsRental && DamageDeduction.HasValue && DamageDeduction.Value > (TotalSellingPrice ?? 0))
                return (false, "Damage deduction cannot exceed selling price");
            
            return (true, "");
        }
        
        /// <summary>
        /// Create a copy of this item (for reuse in other job orders)
        /// </summary>
        public JobOrderItem Clone()
        {
            return new JobOrderItem
            {
                JobOrderId = this.JobOrderId,
                InventoryItemId = this.InventoryItemId,
                ItemName = this.ItemName,
                Sku = this.Sku,
                QuantityUsed = this.QuantityUsed,
                Unit = this.Unit,
                CostPerUnit = this.CostPerUnit,
                SellingPricePerUnit = this.SellingPricePerUnit,
                IsRental = this.IsRental,
                Phase = this.Phase,
                Notes = this.Notes
            };
        }
    }
    
    // ========== Supporting Enums ==========
    
    /// <summary>
    /// Status of each item within a job order
    /// </summary>
    public enum JobOrderItemStatus
    {
        Pending = 1,           // Added but not yet prepared
        Prepared = 2,          // Ready for delivery/pickup
        Delivered = 3,         // Delivered to customer
        Returned = 4,          // Returned (for rentals)
        DamagedReturned = 5,   // Returned with damage (deduction applied)
        Lost = 6,              // Never returned
        Cancelled = 7          // Removed from job order
    }
    
    /// <summary>
    /// Phase of the process this item belongs to
    /// </summary>
    public enum ItemPhase
    {
        Preparation = 1,   // Being prepared at branch
        Loading = 2,       /// Being loaded to vehicle
        Delivery = 3,      // Being delivered to customer
        Installation = 4,  // Being installed on site
        Return = 5,        // Being returned from customer
        Storage = 6        // Back in storage
    }
    
    /// <summary>
    /// Condition of returned rental items
    /// </summary>
    public enum ReturnCondition
    {
        Good = 1,          // No damage
        MinorDamage = 2,   // Minor wear/tear
        MajorDamage = 3,   // Significant damage
        Missing = 4,       // Missing/broken
        Poor = 5           // Unusable condition
    }
}