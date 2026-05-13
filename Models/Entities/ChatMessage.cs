using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities
{
    public class ChatMessage : BaseEntity
    {
        // ========== Properties ==========
        
        [Required]
        public ChatRoomType RoomType { get; set; } // General, Task
        
        /// <summary>
        /// For Task chat: contains TaskId
        /// For General chat: null
        /// </summary>
        public int? RoomId { get; set; } // TaskId if RoomType == Task, else null
        
        [Required]
        public int SenderId { get; set; }
        
        [MaxLength(2000)]
        public string Message { get; set; } = "";
        
        [MaxLength(500)]
        public string? ImageUrl { get; set; }
        
        [MaxLength(500)]
        public string? MentionedUserIds { get; set; } // JSON array or comma-separated
        
        public bool IsRead { get; set; } = false;
        
        // ========== Navigation Properties ==========
        
        [ForeignKey(nameof(SenderId))]
        public virtual User Sender { get; set; } = null!;
        
        // This is the fix - make Task optional and only when RoomType is Task
        [ForeignKey(nameof(RoomId))]
        public virtual Task? Task { get; set; }
    }
}