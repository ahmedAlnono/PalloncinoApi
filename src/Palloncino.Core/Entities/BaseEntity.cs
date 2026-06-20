using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Palloncino.Models.Entities;

public abstract class BaseEntity
{
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        public int? CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }

        /// <summary>
        /// Call this before updating an entity
        /// </summary>
        public void UpdateTimestamp(int? userId = null)
        {
            UpdatedAt = DateTime.UtcNow;
            if (userId.HasValue)
                UpdatedBy = userId.Value;
        }

        /// <summary>
        /// Call this before soft-deleting an entity
        /// </summary>
        public void SoftDelete(int? userId = null)
        {
            IsDeleted = true;
            IsActive = false;
            DeletedAt = DateTime.UtcNow;
            if (userId.HasValue)
                DeletedBy = userId.Value;
        }

        /// <summary>
        /// Call this to restore a soft-deleted entity
        /// </summary>
        public void Restore()
        {
            IsDeleted = false;
            IsActive = true;
            DeletedAt = null;
            DeletedBy = null;
        }

}