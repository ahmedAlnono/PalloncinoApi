using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;

namespace Palloncino.Services.Implementations;

public class UserService(
    ApplicationDbContext context,
    ILogger<UserService> logger) : IUserService
{

    // ========== Authentication & Registration ==========

    public async Task<User?> AuthenticateAsync(string email, string password)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);

        if (user == null)
            return null;

        if (!await ValidatePasswordAsync(user, password))
            return null;

        return user;
    }

    public async Task<User> RegisterCustomerAsync(User user)
    {
        // Validate unique email and phone

        user.Role = UserRole.Customer;
        user.Status = UserStatus.Active;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
        user.CreatedAt = DateTime.UtcNow;

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    public async Task<bool> ValidatePasswordAsync(User user, string password)
    {
        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
            return false;

        if (!await ValidatePasswordAsync(user, currentPassword))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        // Log password change (Activity Log requirement - FR-SEC-02)
        logger.LogInformation("User {UserId} changed password", userId);

        return true;
    }

    // ========== User Management (Admin) ==========

    public async Task<User> CreateStaffUserAsync(User user, UserRole role, int createdBy)
    {
        // Validate branch for staff users
        if (role != UserRole.Customer && user.BranchId > 0)
            throw new InvalidOperationException($"BranchId is required for {role}");

        // Assign role and status
        user.Role = role;
        user.Status = UserStatus.Active;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
        user.CreatedAt = DateTime.UtcNow;
        user.CreatedBy = createdBy;

        context.Users.Add(user);
        await context.SaveChangesAsync();

        logger.LogInformation("Admin {CreatedBy} created new {Role} user: {UserId}", createdBy, role, user.Id);

        return user;
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        var existingUser = await GetUserByIdAsync(user.Id);
        if (existingUser == null)
            throw new InvalidOperationException($"User {user.Id} not found");
        
        if (user.Phone != existingUser.Phone)
            throw new InvalidOperationException($"Phone {user.Phone} already exists");

        // Update fields
        existingUser.FullName = user.FullName;
        existingUser.Email = user.Email;
        existingUser.Phone = user.Phone;
        existingUser.Role = user.Role;
        existingUser.BranchId = user.BranchId;
        existingUser.Status = user.Status;
        existingUser.ProfileImageUrl = user.ProfileImageUrl;
        existingUser.UpdatedAt = DateTime.UtcNow;
        existingUser.UpdatedBy = user.UpdatedBy;

        // If password is provided, hash it
        if (!string.IsNullOrEmpty(user.PasswordHash) && user.PasswordHash != existingUser.PasswordHash)
        {
            existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
        }

        await context.SaveChangesAsync();

        return existingUser;
    }

    public async Task<bool> SoftDeleteUserAsync(int userId, int deletedBy)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
            return false;

        // Don't delete the last admin
        if (user.Role == UserRole.Admin)
        {
            var adminCount = await context.Users.CountAsync(u => u.Role == UserRole.Admin && !u.IsDeleted);
            if (adminCount <= 1)
                throw new InvalidOperationException("Cannot delete the last admin user");
        }

        user.SoftDelete(deletedBy);
        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} soft deleted by {DeletedBy}", userId, deletedBy);

        return true;
    }

    public async Task<bool> HardDeleteUserAsync(int userId)
    {
        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return false;

        context.Users.Remove(user);
        await context.SaveChangesAsync();

        logger.LogWarning("User {UserId} permanently deleted", userId);

        return true;
    }

    public async Task<bool> UpdateUserStatusAsync(int userId, UserStatus status)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
            return false;

        user.Status = status;
        user.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} status updated to {Status}", userId, status);

        return true;
    }

    public async Task<bool> AssignBranchAsync(int userId, int branchId)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
            return false;

        var branchExists = await context.Branches.AnyAsync(b => b.Id == branchId);
        if (!branchExists)
            return false;

        user.BranchId = branchId;
        user.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return true;
    }

    // ========== Queries ==========

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await context.Users
            .Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await context.Users
            .Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
    }

    public async Task<User?> GetUserByPhoneAsync(string phone)
    {
        return await context.Users
            .Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.Phone == phone && !u.IsDeleted);
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync(UserRole? role = null)
    {
        var query = context.Users
            .Include(u => u.Branch)
            .Where(u => !u.IsDeleted);

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        return await query
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetUsersByBranchAsync(int branchId, UserRole? role = null)
    {
        var query = context.Users
            .Include(u => u.Branch)
            .Where(u => u.BranchId == branchId && !u.IsDeleted);

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        return await query
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role)
    {
        return await context.Users
            .Include(u => u.Branch)
            .Where(u => u.Role == role && !u.IsDeleted)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    // ========== Counts ==========

    public async Task<int> GetUserCountAsync(UserRole? role = null)
    {
        var query = context.Users.Where(u => !u.IsDeleted);

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        return await query.CountAsync();
    }

    public async Task<int> GetActiveUsersCountAsync(UserRole? role = null)
    {
        var query = context.Users.Where(u => u.Status == UserStatus.Active && !u.IsDeleted);

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        return await query.CountAsync();
    }

    // ========== Profile Management ==========

    public async Task<User> UpdateProfileAsync(int userId, string fullName, string phone, string? profileImageUrl)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
            throw new InvalidOperationException("User not found");

        // Check phone uniqueness
        if (phone != user.Phone)
            throw new InvalidOperationException("Phone number already in use");

        user.FullName = fullName;
        user.Phone = phone;
        if (profileImageUrl != null)
            user.ProfileImageUrl = profileImageUrl;
        user.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return user;
    }

    public async Task<bool> UpdateLastLoginAsync(int userId)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null)
            return false;

        user.LastLoginAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return true;
    }
}