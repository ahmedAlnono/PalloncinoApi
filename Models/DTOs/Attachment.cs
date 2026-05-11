using Palloncino.Models.Enums;

namespace Palloncino.Models.DTOs
{
    public class UploadAttachmentDto
    {
        public AttachmentType Type { get; set; }
        public int? EntityId { get; set; }
        public string? Description { get; set; }
        public IFormFile File { get; set; } = null!;
    }
    
    public class AttachmentDto : BaseDto
    {
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int UploadedBy { get; set; }
        public string UploadedByName { get; set; } = string.Empty;
        public AttachmentType Type { get; set; }
        public int? EntityId { get; set; }
        public string? Description { get; set; }
    }
}