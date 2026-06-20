using Palloncino.Models.Entities;
using Palloncino.Models.Enums;

namespace Palloncino.Services.Interfaces;

public interface IUserService
{
    // Authentication & Registration
    Task<User?> AuthenticateAsync(string email, string password);
    Task<User> RegisterCustomerAsync(User user);
    Task<bool> ValidatePasswordAsync(User user, string password);
    Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    
    // User Management (Admin)
    Task<User> CreateStaffUserAsync(User user, UserRole role, int createdBy);
    Task<User> UpdateUserAsync(User user);
    Task<bool> SoftDeleteUserAsync(int userId, int deletedBy);
    Task<bool> HardDeleteUserAsync(int userId);
    Task<bool> UpdateUserStatusAsync(int userId, UserStatus status);
    Task<bool> AssignBranchAsync(int userId, int branchId);
    
    // Queries
    Task<User?> GetUserByIdAsync(int userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByPhoneAsync(string phone);
    Task<IEnumerable<User>> GetAllUsersAsync(UserRole? role = null);
    Task<IEnumerable<User>> GetUsersByBranchAsync(int branchId, UserRole? role = null);
    Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role);
    
    // Validation (Unique Checks)
    // Task<bool> EmailExistsAsync(string email, int? excludeUserId = null);
    // Task<bool> PhoneExistsAsync(string phone, int? excludeUserId = null);
    
    // Counts
    Task<int> GetUserCountAsync(UserRole? role = null);
    Task<int> GetActiveUsersCountAsync(UserRole? role = null);
    
    // Profile Management
    Task<User> UpdateProfileAsync(int userId, string fullName, string phone, string? profileImageUrl);
    Task<bool> UpdateLastLoginAsync(int userId);
}