using Palloncino.Data;

namespace Palloncino.Middleware;

public class PostRequestTransactionMiddleware(
    RequestDelegate next,
    ILogger<PostRequestTransactionMiddleware> logger)
{

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        // Only wrap POST requests
        if (context.Request.Method == "POST")
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync();
            
            try
            {
                logger.LogInformation("🚀 Transaction started for POST {Path}", context.Request.Path);
                
                await next(context);
                
                // Check if response is successful
                if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
                {
                    await transaction.CommitAsync();
                    logger.LogInformation("✅ Transaction committed for POST {Path}", context.Request.Path);
                }
                else
                {
                    await transaction.RollbackAsync();
                    logger.LogWarning("⚠️ Transaction rolled back for POST {Path}. Status: {StatusCode}", 
                        context.Request.Path, context.Response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                logger.LogError(ex, "❌ Transaction rolled back for POST {Path} due to exception", context.Request.Path);
                throw;
            }
        }
        else
        {
            await next(context);
        }
    }
}