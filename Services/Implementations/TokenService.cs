using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Palloncino.Models.Entities;


namespace Palloncino.Services.Implementations;

public class TokenService(IConfiguration configuration) : ITokenService
{
    private readonly IConfiguration _configrations = configuration;

    public string GenerateToken(string role, User user)
    {
        var tokenHandeler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_configrations["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt key not found"));

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("name", user.FullName),
            new("email", user.Email),
            new("jti", Guid.NewGuid().ToString()),
            new("role",role),
        };
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configrations["Jwt:DurationInMinutes"])),
            Issuer = _configrations["Jwt:Issuer"],
            Audience = _configrations["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256
            )
        };
        var token = tokenHandeler.CreateToken(tokenDescriptor);
        return tokenHandeler.WriteToken(token);
    }
}

public interface ITokenService
{
    string GenerateToken(string role, User user);
}