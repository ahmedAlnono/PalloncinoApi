using Palloncino.Models.Entities;

namespace Palloncino.Models.Entities;

public class UserDeviceToken : BaseEntity
{
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty; // "ios", "android", "web"
    public string? DeviceModel { get; set; }
    public string? AppVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastUsedAt { get; set; }
    
    // Navigation
    public virtual User User { get; set; } = null!;
}