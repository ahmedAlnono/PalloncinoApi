using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Helpers;
using Palloncino.Models.DTOs;
using Palloncino.Services.Interfaces;

namespace Palloncino.Services.Implementations;

public class AuthService(
    ApplicationDbContext context,
    ILogger<AuthService> logger,
    IPasswordHasher passwordHasher,
    ITokenService tokenService
) : IAuthService
{
    public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto loginDto)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email)
        ?? throw new Exception("user not found");

        if (!passwordHasher.VerifyPassword(loginDto.Password, user.PasswordHash))
            throw new Exception("Wrong Password");

        var token = tokenService.GenerateToken(user.Role.ToString(), user);
        logger.LogInformation("use logged in with email {}", user.Email);
        return new AuthResponseDto
        {
            Token = token,

        };
    }

    public Task<AuthResponseDto?> RegisterAsync(RegisterRequestDto registerDto)
    {
        throw new NotImplementedException();
    }
}