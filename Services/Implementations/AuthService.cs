using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.DTOs;
using Palloncino.Services.Interfaces;

namespace Palloncino.Services.Implementations;

public class AuthService(
    ApplicationDbContext context,
    ILogger<AuthService> logger,
    ITokenService tokenService
) : IAuthService
{
    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto loginDto)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email)
        ?? throw new Exception("user not found");

        if (BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            throw new Exception("Wrong Password");

        var token = tokenService.GenerateTokens(user);

        user.RefreshToken = token.RefreshToken;
        user.RefreshTokenExpiry = DateTime.Now.AddMonths(1);
        logger.LogInformation("use logged in with email {}", user.Email);
        return new AuthResponseDto
        {
            Token = token.AccessToken,
            RefreshToken = token.RefreshToken
        };
    }

    public Task<AuthResponseDto?> RegisterAsync(RegisterRequestDto registerDto)
    {
        throw new NotImplementedException();
    }
}