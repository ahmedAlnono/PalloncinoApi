using Palloncino.Models.Enums;

namespace Palloncino.Models.Entities;
public class Attachment : BaseEntity
{
    // Properties
    public string? FileName { get; set; }
    public string? FileUrl { get; set; }
    public string? FileType { get; set; } // image/jpeg, image/png, etc.
    public long FileSize { get; set; } // in bytes
    public int UploadedBy { get; set; }
    public AttachmentType Type { get; set; } // Order, Task, Design, Checklist
    public int? EntityId { get; set; } // ID of the related entity
    public string? Description { get; set; }
    
    // Navigation Properties
    public virtual User? Uploader { get; set; }
    
    // Validations
    // - FileSize <= 10MB (as per config)
    // - Allowed extensions: .jpg, .jpeg, .png, .pdf
}