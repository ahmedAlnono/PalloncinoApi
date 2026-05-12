using Microsoft.AspNetCore.Identity.Data;
using Palloncino.Models.DTOs;

namespace Palloncino.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto?> RegisterAsync(RegisterRequestDto registerDto);
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto loginDto);
}