using Palloncino.Services.Interfaces;

namespace Palloncino.Middleware;
public class IdempotencyMiddleware(
    RequestDelegate next,
    ILogger<IdempotencyMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<IdempotencyMiddleware> _logger = logger;
    private static readonly HashSet<string> _idempotentMethods = new() { "POST", "PUT", "PATCH" };

    public async Task InvokeAsync(HttpContext context, IIdempotencyService idempotencyService)
    {
        // Step 1: Only process idempotent methods
        if (!_idempotentMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Step 2: Validate Idempotency-Key header
        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var keyHeader))
        {
            _logger.LogWarning("Missing Idempotency-Key header for {Method} {Path}", 
                context.Request.Method, context.Request.Path);
            
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new 
            { 
                error = "Idempotency-Key header is required for POST, PUT, and PATCH requests",
                example = "Idempotency-Key: 123e4567-e89b-12d3-a456-426614174000"
            });
            return;
        }

        var idempotencyKey = keyHeader.ToString();
        var requestType = $"{context.Request.Path}_{context.Request.Method}";

        // Step 3: Check if this request was already processed
        var existingResponse = await idempotencyService.GetExistingResponseAsync(idempotencyKey, requestType);
        
        if (existingResponse != null)
        {
            // Step 4: Return the SAME response as before
            _logger.LogInformation("Idempotent request detected! Returning cached response for key {Key}", 
                idempotencyKey);
            
            context.Response.StatusCode = existingResponse.StatusCode;
            context.Response.ContentType = "application/json";
            
            // Add header to indicate this is a cached response
            context.Response.Headers.Append("X-Idempotent-Result", "cached");
            
            await context.Response.WriteAsync(existingResponse.ResponseBody);
            return; // ← Controller is NEVER called!
        }

        // Step 5: Capture the response (first time request)
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            // Execute the actual controller
            await _next(context);
            
            // Read what the controller wrote
            responseBody.Seek(0, SeekOrigin.Begin);
            var responseContent = await new StreamReader(responseBody).ReadToEndAsync();
            
            // Store the response for future identical requests
            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 500)
            {
                await idempotencyService.SaveResponseAsync(
                    idempotencyKey, 
                    requestType, 
                    context.Response.StatusCode, 
                    responseContent);
                
                _logger.LogInformation("Saved idempotent response for key {Key}", idempotencyKey);
            }
            
            // Copy response back to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            // Don't store failed responses
            _logger.LogError(ex, "Error processing request for key {Key}", idempotencyKey);
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}