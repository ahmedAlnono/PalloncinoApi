using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Palloncino.Data;
using Palloncino.Helpers;
using Palloncino.Services.Implementations;
using Palloncino.Services.Interfaces;
using Palloncino.Mappers;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

// ==== Add logger =====
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/palloncino-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();
builder.Host.UseSerilog();


// ====== Add Controller =========

builder.Services.AddControllers()
           .AddJsonOptions(options =>
           {
               options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
               options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
           });

builder.Services.AddEndpointsApiExplorer();



// ========= Add swagger =========
builder.Services.AddOpenApi(options =>
{
    // Add document info
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "Palloncino Management System API",
            Version = "v1",
            Description = "API for Palloncino Party Management System"
        };

        // Add JWT security scheme
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT token"
        };

        // Add security requirement
        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document, externalResource: null)] = new List<string>()
        });

        return Task.CompletedTask;
    });
});


// ========== Add Database ================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString, sqliteOptions =>
    {
        sqliteOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name);
        sqliteOptions.CommandTimeout(60);
    }));


// ========== 4. Configure JWT Authentication ==========
var jwtKey = builder.Configuration["Jwt:Key"] ?? "PalloncinoSuperSecretKey2024Min32Chars!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PalloncinoAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PalloncinoApp";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

    // For SignalR or WebSocket support if needed later
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chat"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// ========== 5. Configure Authorization Policies ==========
builder.Services.AddAuthorizationBuilder()

    .AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"))

    .AddPolicy("EmployeeOnly", policy =>
        policy.RequireRole("Admin", "Employee"))

    .AddPolicy("DriverOnly", policy =>
        policy.RequireRole("Admin", "Driver"))

    .AddPolicy("DesignerOnly", policy =>
        policy.RequireRole("Admin", "Designer"))

    .AddPolicy("InternalStaff", policy =>
        policy.RequireRole("Admin", "Employee", "Designer", "Driver"))

    .AddPolicy("CustomerOnly", policy =>
        policy.RequireRole("Customer"))

    .AddPolicy("SameBranch", policy =>
        policy.RequireAssertion(context =>
        {
            var userBranchId = context.User.FindFirst("branchId")?.Value;
            var resourceBranchId = context.Resource?.ToString();
            return userBranchId == resourceBranchId || context.User.IsInRole("Admin");
        }));


// ========== 6. Register Custom Services ==========
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IBranchService, BranchService>();


// builder.Services.AddScoped<IJobOrderService, JobOrderService>();
// builder.Services.AddScoped<INotificationService, NotificationService>();
// builder.Services.AddScoped<IReportService, ReportService>();
// builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
// builder.Services.AddScoped<IInventoryService, InventoryService>();
// builder.Services.AddScoped<IOrderService, OrderService>();
// builder.Services.AddScoped<ITaskService, TaskService>();
// builder.Services.AddScoped<IDeliveryService, DeliveryService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
                "https://palloncino.com",
                "https://admin.palloncino.com",
                "https://api.palloncino.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// ========== 8. Configure Response Caching ==========
builder.Services.AddResponseCaching();

// ========== 9. Configure Memory Cache ==========
builder.Services.AddMemoryCache();

builder.Services.AddAutoMapper(typeof(AutoMapperProfile));

builder.Services.AddRateLimiter(options =>
{
    // Global policy - applies to all endpoints if no specific policy is used
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers["X-Forwarded-For"].ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Specific policy for authenticated users
    options.AddPolicy("Authenticated", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 200,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Strict policy for login/register endpoints
    options.AddPolicy("Strict", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(5)
            }));

    // Policy for mobile app API (higher limits)
    options.AddPolicy("MobileAPI", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ??
                          httpContext.Request.Headers["X-Device-Id"].ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 300,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Policy for delivery/driver endpoints
    options.AddPolicy("DriverAPI", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name?.ToString() ?? "driver",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 500,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Configure rejection response
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";

        TimeSpan retryAfter = TimeSpan.FromSeconds(60);
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan metadataRetryAfter))
        {
            retryAfter = metadataRetryAfter;
        }

        var response = new
        {
            status = 429,
            message = "Too many requests. Please try again later.",
            retryAfter = retryAfter.TotalSeconds
        };

        await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
    };
});


var app = builder.Build();

// ========== 10. Apply Database Migrations ==========
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying database migrations...");
        dbContext.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully.");

        // Optional: Seed initial data
        // await DbInitializer.InitializeAsync(dbContext);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying migrations.");
        throw;
    }
}


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("AllowAll");
}

app.UseStaticFiles();
app.UseResponseCaching();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.UseRateLimiter();


if (app.Environment.IsProduction())
{
    app.UseCors("Production");
    app.UseHttpsRedirection();
}



try
{
    Log.Information("Starting Palloncino API...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
