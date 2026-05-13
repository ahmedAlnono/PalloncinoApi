using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Palloncino.Models.Entities;

namespace Palloncino.Services.Implementations;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    
    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // ========== Generate Access Token ==========
    public string GenerateAccessToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] 
            ?? throw new InvalidOperationException("JWT Key not found"));
        
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("name", user.FullName),
            new("email", user.Email),
            new("jti", Guid.NewGuid().ToString()),
            new("role", user.Role.ToString()),
            new("userId", user.Id.ToString()),
            new("branchId", user.BranchId.ToString() ?? ""),
        };
        
        // Add branch-based claims for multi-branch support (Section 2.3 of SRS)
        if (user.BranchId != 0)
        {
            claims.Add(new Claim("branchId", user.BranchId.ToString()));
            claims.Add(new Claim("branchName", user.Branch?.Name ?? ""));
        }
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpiryInMinutes"] ?? "60")),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    
    // ========== Generate Refresh Token ==========
    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
    
    // ========== Generate Both Tokens ==========
    public TokenResponse GenerateTokens(User user)
    {
        return new TokenResponse
        {
            AccessToken = GenerateAccessToken(user),
            RefreshToken = GenerateRefreshToken(),
            ExpiresIn = Convert.ToInt32(_configuration["Jwt:ExpiryInMinutes"] ?? "60"),
            TokenType = "Bearer",
            UserId = user.Id,
            UserName = user.FullName,
            UserEmail = user.Email,
            UserRole = user.Role.ToString()
        };
    }
    
    // ========== Validate Refresh Token ==========
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] 
            ?? throw new InvalidOperationException("JWT Key not found"));
        
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = false, // Don't validate lifetime for expired token
                ClockSkew = TimeSpan.Zero
            }, out _);
            
            return principal;
        }
        catch
        {
            return null;
        }
    }
    
    // ========== Validate Access Token (without expiration) ==========
    public bool ValidateToken(string token, out ClaimsPrincipal? principal)
    {
        principal = null;
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] 
            ?? throw new InvalidOperationException("JWT Key not found"));
        
        try
        {
            principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);
            
            return true;
        }
        catch
        {
            return false;
        }
    }
}