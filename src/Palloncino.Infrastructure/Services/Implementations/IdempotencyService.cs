using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.Entities;
using Palloncino.Services.Interfaces;
using Task = System.Threading.Tasks.Task;
namespace Palloncino.Services.Implementations;

// Database implementation
public class DatabaseIdempotencyService(
    ApplicationDbContext context,
    ILogger<DatabaseIdempotencyService> logger) : IIdempotencyService
{

    public async Task<StoredResponse?> GetExistingResponseAsync(string key, string requestType)
    {
        var record = await context.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == key && r.RequestType == requestType);
        
        if (record == null) return null;
        
        return new StoredResponse
        {
            Key = record.Key,
            RequestType = record.RequestType,
            StatusCode = record.StatusCode,
            ResponseBody = record.ResponseBody,
            CreatedAt = record.CreatedAt
        };
    }

    public async Task SaveResponseAsync(string key, string requestType, int statusCode, string responseBody)
    {
        var record = new IdempotencyRecord
        {
            Key = key,
            RequestType = requestType,
            StatusCode = statusCode,
            ResponseBody = responseBody,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow,
            IsCompleted = true
        };
        
        context.IdempotencyRecords.Add(record);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Saved idempotent response for key {Key}", key);
    }

    public async Task CleanupOldRecordsAsync(int daysToKeep = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
        var oldRecords = context.IdempotencyRecords.Where(r => r.CreatedAt < cutoff);
        var count = await oldRecords.ExecuteDeleteAsync();
        logger.LogInformation("Cleaned up {Count} old idempotency records", count);
    }
}