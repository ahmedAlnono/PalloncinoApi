// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using Palloncino.Models.Entities;
// using Palloncino.Models.Enums;
// using Palloncino.Services.Interfaces;

// namespace Palloncino.Controllers;

// [ApiController]
// [Route("api/[controller]")]
// public class AuthController : ControllerBase
// {
//     private readonly IUserService _userService;
//     private readonly ITokenService _tokenService;
//     private readonly ILogger<AuthController> _logger;
    
//     public AuthController(
//         IUserService userService,
//         ITokenService tokenService,
//         ILogger<AuthController> logger)
//     {
//         _userService = userService;
//         _tokenService = tokenService;
//         _logger = logger;
//     }
    
//     [HttpPost("login")]
//     public async Task<IActionResult> Login([FromBody] LoginRequest request)
//     {
//         var user = await _userService.AuthenticateAsync(request.Email, request.Password);
        
//         if (user == null)
//         {
//             return Unauthorized(new { message = "Invalid email or password" });
//         }
        
//         if (user.Status != UserStatus.Active)
//         {
//             return Unauthorized(new { message = "Account is inactive. Please contact support." });
//         }
        
//         // Generate tokens
//         var tokens = _tokenService.GenerateTokens(user);
        
//         // Save refresh token to database (you need to add RefreshToken property to User entity)
//         user.RefreshToken = tokens.RefreshToken;
//         user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7); // Refresh token valid for 7 days
//         user.LastLoginAt = DateTime.UtcNow;
//         await _userService.UpdateUserAsync(user);
        
//         // Log login activity (BR-08 requires activity logging)
//         _logger.LogInformation("User {UserId} logged in successfully", user.Id);
        
//         return Ok(new
//         {
//             tokens.AccessToken,
//             tokens.RefreshToken,
//             tokens.ExpiresIn,
//             tokens.TokenType,
//             user = new
//             {
//                 user.Id,
//                 user.FullName,
//                 user.Email,
//                 user.Phone,
//                 user.Role,
//                 user.BranchId
//             }
//         });
//     }
    
//     [HttpPost("refresh")]
//     public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
//     {
//         // Get principal from expired token
//         var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        
//         if (principal == null)
//         {
//             return BadRequest(new { message = "Invalid access token" });
//         }
        
//         var userId = int.Parse(principal.FindFirst("userId")?.Value ?? "0");
//         var user = await _userService.GetUserByIdAsync(userId);
        
//         if (user == null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiry <= DateTime.UtcNow)
//         {
//             return Unauthorized(new { message = "Invalid or expired refresh token" });
//         }
        
//         // Generate new tokens
//         var newTokens = _tokenService.GenerateTokens(user);
        
//         // Update refresh token in database
//         user.RefreshToken = newTokens.RefreshToken;
//         user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
//         await _userService.UpdateUserAsync(user);
        
//         return Ok(new
//         {
//             newTokens.AccessToken,
//             newTokens.RefreshToken,
//             newTokens.ExpiresIn,
//             newTokens.TokenType
//         });
//     }
    
//     [Authorize]
//     [HttpPost("logout")]
//     public async Task<IActionResult> Logout()
//     {
//         var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
//         var user = await _userService.GetUserByIdAsync(userId);
        
//         if (user != null)
//         {
//             user.RefreshToken = null;
//             user.RefreshTokenExpiry = null;
//             await _userService.UpdateUserAsync(user);
//         }
        
//         return Ok(new { message = "Logged out successfully" });
//     }
    
//     [Authorize]
//     [HttpPost("change-password")]
//     public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
//     {
//         var userId = int.Parse(User.FindFirst("userId")?.Value ?? "0");
//         var result = await _userService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        
//         if (!result)
//         {
//             return BadRequest(new { message = "Current password is incorrect" });
//         }
        
//         return Ok(new { message = "Password changed successfully" });
//     }
    
//     // ========== Request DTOs ==========
    
//     public class LoginRequest
//     {
//         public string Email { get; set; } = string.Empty;
//         public string Password { get; set; } = string.Empty;
//     }
    
//     public class RefreshTokenRequest
//     {
//         public string AccessToken { get; set; } = string.Empty;
//         public string RefreshToken { get; set; } = string.Empty;
//     }
    
//     public class ChangePasswordRequest
//     {
//         public string CurrentPassword { get; set; } = string.Empty;
//         public string NewPassword { get; set; } = string.Empty;
//     }
// }