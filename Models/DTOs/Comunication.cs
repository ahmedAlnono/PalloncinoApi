using System.ComponentModel.DataAnnotations;
using Palloncino.Models.Enums;

namespace Palloncino.Models.DTOs
{
    // ========== Notification DTOs ==========
    
    public class SendNotificationDto
    {
        [Required]
        public int RecipientId { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = "";
        
        [Required]
        [MaxLength(1000)]
        public string Body { get; set; } = "";
        
        public NotificationType Type { get; set; } = NotificationType.General;
        
        public string? ImageUrl { get; set; }
        
        public int? RelatedEntityId { get; set; }
        
        public string? RelatedEntityType { get; set; }
    }
    
    public class BroadcastNotificationDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = "";
        
        [Required]
        [MaxLength(1000)]
        public string Body { get; set; } = "";
        
        public NotificationType Type { get; set; } = NotificationType.General;
        
        public string? ImageUrl { get; set; }
        
        public List<UserRole>? TargetRoles { get; set; } // Null = all users
    }
    
    public class NotificationDto : BaseDto
    {
        public int RecipientId { get; set; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public NotificationType Type { get; set; }
        public string? ImageUrl { get; set; }
        public int? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime? SentAt { get; set; }
    }
    
    // ========== Chat DTOs ==========
    
    public class SendChatMessageDto
    {
        [Required]
        public ChatRoomType RoomType { get; set; }
        
        public int? RoomId { get; set; } // TaskId if RoomType == Task
        
        [MaxLength(2000)]
        public string? Message { get; set; }
        
        public IFormFile? Image { get; set; }
        
        public List<int>? MentionedUserIds { get; set; }
    }
    
    public class ChatMessageDto : BaseDto
    {
        public ChatRoomType RoomType { get; set; }
        public int? RoomId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = "";
        public string? SenderImageUrl { get; set; }
        public string? Message { get; set; }
        public string? ImageUrl { get; set; }
        public List<int>? MentionedUserIds { get; set; }
        public bool IsRead { get; set; }
    }
}