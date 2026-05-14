using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;
using Palloncino.Data;
using Microsoft.EntityFrameworkCore;



namespace Palloncino.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    IUserService userService,
    ITokenService tokenService,
    IBranchService branchService,
    ApplicationDbContext context,
    ILogger<AuthController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request)
    {
        var user = await userService.AuthenticateAsync(request.Email, request.Password);

        if (user == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        if (user.Status != UserStatus.Active)
        {
            return Unauthorized(new { message = "Account is inactive. Please contact support." });
        }

        // Generate tokens
        var tokens = tokenService.GenerateTokens(user);

        // Save refresh token to database (you need to add RefreshToken property to User entity)
        user.RefreshToken = tokens.RefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7); // Refresh token valid for 7 days
        user.LastLoginAt = DateTime.UtcNow;
        await userService.UpdateUserAsync(user);

        // Log login activity (BR-08 requires activity logging)
        logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return Ok(new
        {
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresIn,
            tokens.TokenType,
            user = new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Phone,
                user.Role,
                user.BranchId
            }
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        // Get principal from expired token
        var principal = tokenService.GetPrincipalFromExpiredToken(request.AccessToken);

        if (principal == null)
        {
            return BadRequest(new { message = "Invalid access token" });
        }

        var userId = int.Parse(principal.FindFirst("userId")?.Value ?? "0");
        var user = await userService.GetUserByIdAsync(userId);

        if (user == null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiry <= DateTime.UtcNow)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token" });
        }

        // Generate new tokens
        var newTokens = tokenService.GenerateTokens(user);

        // Update refresh token in database
        user.RefreshToken = newTokens.RefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddMonths(1);
        await userService.UpdateUserAsync(user);

        return Ok(new
        {
            newTokens.AccessToken,
            newTokens.RefreshToken,
            newTokens.ExpiresIn,
            newTokens.TokenType
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
        var user = await userService.GetUserByIdAsync(userId);

        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
            await userService.UpdateUserAsync(user);
        }

        return Ok(new { message = "Logged out successfully" });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
        var result = await userService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);

        if (!result)
        {
            return BadRequest(new { message = "Current password is incorrect" });
        }

        return Ok(new { message = "Password changed successfully" });
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        // Create new customer user
        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Customer,  // Fixed: Always Customer
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        await userService.RegisterCustomerAsync(user);

        // Generate tokens
        var tokens = tokenService.GenerateTokens(user);

        return Ok(new
        {
            message = "Registration successful",
            tokens.AccessToken,
            tokens.RefreshToken,
            user = new { user.Id, user.FullName, user.Email, user.Role }
        });
    }


    [Authorize(Roles = "Admin")]
    [HttpPost("users/employee")]
    public async Task<IActionResult> CreateEmployee([FromBody] RegisterRequestDto request)
    {
        // Validate branch exists
        if (request.BranchId is null)
        {
            throw new Exception("Branch Id is Required");
        }
        var branch = await branchService.GetBranchByIdAsync((int)request.BranchId);
        if (branch == null)
            return BadRequest(new { message = "Branch not found" });

        // Create employee
        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Employee,  // Fixed: Employee
            BranchId = request.BranchId,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId()
        };

        // await userService.CreateUserAsync(user);
        await userService.CreateStaffUserAsync(user, user.Role, (int)user.CreatedBy);

        // Send welcome email/notification
        // await notificationService.SendWelcomeNotification(user);

        return Ok(new
        {
            message = "Employee created successfully",
            user = new { user.Id, user.FullName, user.Email, user.Role, user.BranchId }
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("users/designer")]
    public async Task<IActionResult> CreateDesigner([FromBody] RegisterRequestDto request)
    {
        if (request.BranchId == null)
            throw new Exception("BranchId is required");
        var branch = await branchService.GetBranchByIdAsync((int)request.BranchId);
        if (branch == null)
            return BadRequest(new { message = "Branch not found" });


        // Create designer
        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Designer,  // Fixed: Designer
            BranchId = request.BranchId,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId()
        };

        await userService.CreateStaffUserAsync(user, user.Role, GetCurrentUserId());

        return Ok(new
        {
            message = "Designer created successfully",
            user = new { user.Id, user.FullName, user.Email, user.Role, user.BranchId }
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("users/driver")]
    public async Task<IActionResult> CreateDriver([FromBody] CreateDriverDto request)
    {
        if(request.BranchId == null)
            throw new Exception("branch is required");
        var branch = await branchService.GetBranchByIdAsync((int)request.BranchId);
        if (branch == null)
            return BadRequest(new { message = "Branch not found" });

        // Create driver
        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Driver,  // Fixed: Driver
            BranchId = request.BranchId,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = GetCurrentUserId()
        };

        await userService.CreateStaffUserAsync(user, user.Role, GetCurrentUserId());

        return Ok(new
        {
            message = "Driver created successfully",
            user = new { user.Id, user.FullName, user.Email, user.Role, user.BranchId }
        });
    }


    [HttpGet("test")]
    public ActionResult Test()
    {
        return Ok(GetCurrentUserId());
    }

    /// <summary>
    /// Register device token for push notifications
    /// </summary>
    [Authorize]
    [HttpPost("device-token")]
    public async Task<IActionResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenRequest request)
    {
        var userId = GetCurrentUserId();

        var existingToken = await context.UserDeviceTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == request.Token);

        if (existingToken == null)
        {
            var deviceToken = new UserDeviceToken
            {
                UserId = userId,
                Token = request.Token,
                DeviceType = request.DeviceType,
                DeviceModel = request.DeviceModel,
                AppVersion = request.AppVersion,
                IsActive = true,
                LastUsedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            context.UserDeviceTokens.Add(deviceToken);
        }
        else
        {
            existingToken.LastUsedAt = DateTime.UtcNow;
            existingToken.IsActive = true;
            existingToken.DeviceType = request.DeviceType;
            existingToken.DeviceModel = request.DeviceModel;
            existingToken.AppVersion = request.AppVersion;
            existingToken.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();

        return Ok(new { success = true, message = "Device token registered successfully" });
    }

    public class RegisterDeviceTokenRequest
    {
        public string Token { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string? DeviceModel { get; set; }
        public string? AppVersion { get; set; }
    }

    public class CreateDriverDto : RegisterRequestDto
    {
        public string? DriverLicenseNumber { get; set; }
        public string? VehiclePlateNumber { get; set; }
    }
    private int GetCurrentUserId()
    {
        string? id = (User.Claims.FirstOrDefault(u => u.Type == "sub")?.Value)
        ?? throw new Exception("Id not found");
        return int.Parse(id);
    }
}