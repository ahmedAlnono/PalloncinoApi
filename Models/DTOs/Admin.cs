using System.ComponentModel.DataAnnotations;
using Palloncino.Models.Enums;

namespace Palloncino.Models.DTOs
{
    // ========== Branch DTOs ==========
    
    public class CreateBranchDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;
        
        [Phone]
        [MaxLength(20)]
        public string? Phone { get; set; }
        
        [MaxLength(200)]
        public string? ManagerName { get; set; }
    }
    
    public class UpdateBranchDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }
        
        [MaxLength(500)]
        public string? Address { get; set; }
        
        [Phone]
        [MaxLength(20)]
        public string? Phone { get; set; }
        
        [MaxLength(200)]
        public string? ManagerName { get; set; }
        
        public BranchStatus? Status { get; set; }
    }
    
    public class BranchDto : BaseDto
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? ManagerName { get; set; }
        public BranchStatus Status { get; set; }
        public int EmployeeCount { get; set; }
    }
    
    // ========== User Management DTOs ==========
    
    public class CreateUserDto
    {
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;
        
        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
        
        [Required]
        public UserRole Role { get; set; }
        
        public int? BranchId { get; set; }
    }
    
    public class UpdateUserDto
    {
        [MaxLength(200)]
        public string? FullName { get; set; }
        
        [Phone]
        public string? Phone { get; set; }
        
        public UserRole? Role { get; set; }
        
        public int? BranchId { get; set; }
        
        public UserStatus? Status { get; set; }
    }
    
    public class AssignRoleDto
    {
        [Required]
        public UserRole Role { get; set; }
        
        public int? BranchId { get; set; }
    }
}