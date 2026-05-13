namespace Palloncino.Models.DTOs
{
    public class ActivityLogDto : BaseDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public string Action { get; set; } = "";
        public string EntityType { get; set; } = "";
        public int EntityId { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? IpAddress { get; set; }
        public string? Notes { get; set; }
    }
    
    public class ActivityLogFilterDto
    {
        public string? EntityType { get; set; }
        public int? EntityId { get; set; }
        public int? UserId { get; set; }
        public string? Action { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}