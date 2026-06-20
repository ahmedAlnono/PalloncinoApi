namespace Palloncino.Models.Entities;
using Palloncino.Models.Entities;

public class IdempotencyRecord : BaseEntity
{
    public string Key { get; set; } = string.Empty;           // Client-provided UUID
    public string RequestType { get; set; } = string.Empty;   // "/api/orders_POST"
    public int StatusCode { get; set; }                       // 200, 201, 400, etc.
    public string ResponseBody { get; set; } = string.Empty;   // JSON response
    public DateTime? ProcessedAt { get; set; }                // When stored
    public bool IsCompleted { get; set; }                     // Successfully stored
    
    public string? RequestHash { get; set; }                  // To validate same request body
}