using Microsoft.EntityFrameworkCore;
using Palloncino.Data;

namespace Palloncino.BackgroundJobs;
public class IdempotencyCleanupService(IServiceProvider services, ILogger<IdempotencyCleanupService> logger) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var cutoff = DateTime.UtcNow.AddDays(-30);
            var oldRecords = context.IdempotencyRecords.Where(r => r.CreatedAt < cutoff);
            
            var count = await oldRecords.ExecuteDeleteAsync(stoppingToken);
            logger.LogInformation("Cleaned up {Count} old idempotency records", count);
        }
    }
}