using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;
using Palloncino.Models.DTOs;
namespace Palloncino.Services.Implementations;

public class BranchService(
    ApplicationDbContext context,
    ILogger<BranchService> logger) : IBranchService
{
    // ========== CRUD Operations ==========
    
    public async Task<Branch> CreateBranchAsync(Branch branch)
    {
        // Validate unique name
        if (await BranchNameExistsAsync(branch.Name))
            throw new InvalidOperationException($"Branch name '{branch.Name}' already exists");
        
        branch.Status = BranchStatus.Active;
        branch.CreatedAt = DateTime.UtcNow;
        branch.IsActive = true;
        
        context.Branches.Add(branch);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Branch created: {BranchName} (ID: {BranchId})", branch.Name, branch.Id);
        
        return branch;
    }
    
    public async Task<Branch> UpdateBranchAsync(Branch branch)
    {
        var existingBranch = await GetBranchByIdAsync(branch.Id);
        if (existingBranch == null)
            throw new InvalidOperationException($"Branch with ID {branch.Id} not found");
        
        // Check name uniqueness
        if (branch.Name != existingBranch.Name && await BranchNameExistsAsync(branch.Name?? "", branch.Id))
            throw new InvalidOperationException($"Branch name '{branch.Name}' already exists");
        
        // Update fields
        existingBranch.Name = branch.Name;
        existingBranch.Address = branch.Address;
        existingBranch.Phone = branch.Phone;
        existingBranch.ManagerName = branch.ManagerName;
        existingBranch.Status = branch.Status;
        existingBranch.UpdatedAt = DateTime.UtcNow;
        existingBranch.UpdatedBy = branch.UpdatedBy;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Branch updated: {BranchName} (ID: {BranchId})", existingBranch.Name, existingBranch.Id);
        
        return existingBranch;
    }
    
    public async Task<bool> DeleteBranchAsync(int branchId)
    {
        var branch = await GetBranchByIdAsync(branchId);
        if (branch == null)
            return false;
        
        // Check if branch has users
        var hasUsers = await context.Users.AnyAsync(u => u.BranchId == branchId && !u.IsDeleted);
        if (hasUsers)
            throw new InvalidOperationException($"Cannot delete branch with assigned users. Reassign users first.");
        
        // Check if branch has job orders
        var hasJobOrders = await context.JobOrders.AnyAsync(j => j.BranchId == branchId);
        if (hasJobOrders)
            throw new InvalidOperationException($"Cannot delete branch with existing job orders.");
        
        context.Branches.Remove(branch);
        await context.SaveChangesAsync();
        
        logger.LogWarning("Branch permanently deleted: {BranchName} (ID: {BranchId})", branch.Name, branchId);
        
        return true;
    }
    
    public async Task<bool> SoftDeleteBranchAsync(int branchId, int deletedBy)
    {
        var branch = await GetBranchByIdAsync(branchId);
        if (branch == null)
            return false;
        
        branch.SoftDelete(deletedBy);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Branch soft deleted: {BranchName} (ID: {BranchId}) by User {DeletedBy}", 
            branch.Name, branchId, deletedBy);
        
        return true;
    }
    
    // ========== Queries ==========
    
    public async Task<Branch?> GetBranchByIdAsync(int branchId)
    {
        return await context.Branches
            .FirstOrDefaultAsync(b => b.Id == branchId && !b.IsDeleted);
    }
    
    public async Task<Branch?> GetBranchByNameAsync(string name)
    {
        return await context.Branches
            .FirstOrDefaultAsync(b => b.Name == name && !b.IsDeleted);
    }
    
    public async Task<IEnumerable<Branch>> GetAllBranchesAsync(bool includeInactive = false)
    {
        var query = context.Branches.Where(b => !b.IsDeleted);
        
        if (!includeInactive)
            query = query.Where(b => b.Status == BranchStatus.Active);
        
        return await query
            .OrderBy(b => b.Name)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Branch>> GetActiveBranchesAsync()
    {
        return await context.Branches
            .Where(b => b.Status == BranchStatus.Active && !b.IsDeleted)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }
    
    // ========== Validation ==========
    
    public async Task<bool> BranchExistsAsync(int branchId)
    {
        return await context.Branches
            .AnyAsync(b => b.Id == branchId && !b.IsDeleted);
    }
    
    public async Task<bool> BranchNameExistsAsync(string name, int? excludeBranchId = null)
    {
        var query = context.Branches.Where(b => b.Name == name && !b.IsDeleted);
        
        if (excludeBranchId.HasValue)
            query = query.Where(b => b.Id != excludeBranchId.Value);
        
        return await query.AnyAsync();
    }
    
    // ========== Branch Statistics ==========
    
    public async Task<int> GetBranchEmployeeCountAsync(int branchId)
    {
        return await context.Users
            .CountAsync(u => u.BranchId == branchId 
                && u.Role != UserRole.Customer 
                && u.Status == UserStatus.Active 
                && !u.IsDeleted);
    }
    
    public async Task<int> GetBranchActiveJobOrdersCountAsync(int branchId)
    {
        return await context.JobOrders
            .CountAsync(j => j.BranchId == branchId 
                && j.Status != JobOrderStatus.Completed 
                && j.Status != JobOrderStatus.Cancelled);
    }
    
    public async Task<BranchStatisticsDto> GetBranchStatisticsAsync(int branchId)
    {
        var branch = await GetBranchByIdAsync(branchId);
        if (branch == null)
            throw new InvalidOperationException($"Branch with ID {branchId} not found");
        
        var employees = await GetBranchEmployeeCountAsync(branchId);
        var activeJobOrders = await GetBranchActiveJobOrdersCountAsync(branchId);
        
        var completedJobOrdersThisMonth = await context.JobOrders
            .CountAsync(j => j.BranchId == branchId 
                && j.Status == JobOrderStatus.Completed 
                && j.UpdatedAt >= DateTime.UtcNow.AddMonths(-1));
        
        var totalRevenueThisMonth = await context.JobOrders
            .Where(j => j.BranchId == branchId 
                && j.Status == JobOrderStatus.Completed 
                && j.UpdatedAt >= DateTime.UtcNow.AddMonths(-1))
            .SumAsync(j => j.TotalRevenue);
        
        return new BranchStatisticsDto
        {
            BranchId = branchId,
            BranchName = branch.Name?? "",
            EmployeeCount = employees,
            ActiveJobOrdersCount = activeJobOrders,
            CompletedJobOrdersThisMonth = completedJobOrdersThisMonth,
            TotalRevenueThisMonth = totalRevenueThisMonth
        };
    }
    
    // ========== Status Management ==========
    
    public async Task<bool> ActivateBranchAsync(int branchId)
    {
        var branch = await GetBranchByIdAsync(branchId);
        if (branch == null)
            return false;
        
        branch.Status = BranchStatus.Active;
        branch.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Branch activated: {BranchName} (ID: {BranchId})", branch.Name, branchId);
        
        return true;
    }
    
    public async Task<bool> DeactivateBranchAsync(int branchId)
    {
        var branch = await GetBranchByIdAsync(branchId);
        if (branch == null)
            return false;
        
        // Check if branch has active job orders
        var hasActiveJobOrders = await context.JobOrders
            .AnyAsync(j => j.BranchId == branchId 
                && j.Status != JobOrderStatus.Completed 
                && j.Status != JobOrderStatus.Cancelled);
        
        if (hasActiveJobOrders)
            throw new InvalidOperationException($"Cannot deactivate branch with active job orders.");
        
        branch.Status = BranchStatus.Inactive;
        branch.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Branch deactivated: {BranchName} (ID: {BranchId})", branch.Name, branchId);
        
        return true;
    }
    
    // ========== User Assignment ==========
    
    public async Task<bool> AssignUserToBranchAsync(int userId, int branchId)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null)
            throw new InvalidOperationException($"User with ID {userId} not found");
        
        var branch = await GetBranchByIdAsync(branchId);
        if (branch == null)
            throw new InvalidOperationException($"Branch with ID {branchId} not found");
        
        // Only staff users can be assigned to branches
        if (user.Role == UserRole.Customer)
            throw new InvalidOperationException("Customers cannot be assigned to branches");
        
        user.BranchId = branchId;
        user.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("User {UserId} assigned to branch {BranchId}", userId, branchId);
        
        return true;
    }
    
    public async Task<bool> RemoveUserFromBranchAsync(int userId)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null)
            return false;
        
        user.BranchId = null;
        user.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("User {UserId} removed from branch assignment", userId);
        
        return true;
    }
}