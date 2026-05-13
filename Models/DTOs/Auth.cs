using System.ComponentModel.DataAnnotations;
using Palloncino.Models.Enums;

namespace Palloncino.Models.DTOs
{
    // ========== Request DTOs ==========

    public class LoginRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = "";
    }

    public class RegisterRequestDto
    {
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [Phone]
        [MaxLength(20)]
        public string Phone { get; set; } = "";

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = "";

        [Compare("Password")]
        public string ConfirmPassword { get; set; } = "";

        public int? BranchId { get; set; }
    }

    public class RefreshTokenRequestDto
    {
        [Required]
        public string RefreshToken { get; set; } = "";

        [Required]
        public string AccessToken {get;set;} = "";
    }

    public class ChangePasswordRequestDto
    {
        [Required]
        public string CurrentPassword { get; set; } = "";

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = "";

        [Compare("NewPassword")]
        public string ConfirmNewPassword { get; set; } = "";
    }

    public class ForgotPasswordRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";
    }

    public class ResetPasswordRequestDto
    {
        [Required]
        public string Token { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = "";

        [Compare("NewPassword")]
        public string ConfirmNewPassword { get; set; } = "";
    }

    // ========== Response DTOs ==========

    public class AuthResponseDto
    {
        public string Token { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; } = null!;
    }

    public class UserDto : BaseDto
    {
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public UserRole Role { get; set; }
        public string? ProfileImageUrl { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public UserStatus Status { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class UserListDto : BaseDto
    {
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public UserRole Role { get; set; }
        public UserStatus Status { get; set; }
        public string? BranchName { get; set; }
    }
}