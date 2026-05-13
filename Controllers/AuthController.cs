using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palloncino.Helpers;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;

namespace Palloncino.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    IUserService userService,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
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
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
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
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
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
    public async Task<IActionResult> Register([FromBody] CustomerRegisterDto request)
    {
        // Create new customer user
        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = passwordHasher.HashPassword(request.Password),
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

    // DTO
    public class CustomerRegisterDto
    {
        [Required]
        public string FullName { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [Phone]
        public string Phone { get; set; } = "";

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = "";
    }
    public class RefreshTokenRequest
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}