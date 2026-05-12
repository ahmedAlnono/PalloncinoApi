// ========== Interface ==========
using System.Security.Claims;
using Palloncino.Models.Entities;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    TokenResponse GenerateTokens(User user);
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
    bool ValidateToken(string token, out ClaimsPrincipal? principal);
}

// ========== Response DTO ==========
public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; } // seconds
    public string TokenType { get; set; } = "Bearer";
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
}
