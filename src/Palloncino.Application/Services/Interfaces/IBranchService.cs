using Palloncino.Models.Entities;
using Palloncino.Models.DTOs;

namespace Palloncino.Services.Interfaces;

public interface IBranchService
{
    // CRUD Operations
    Task<Branch> CreateBranchAsync(Branch branch);
    Task<Branch> UpdateBranchAsync(Branch branch);
    Task<bool> DeleteBranchAsync(int branchId);
    Task<bool> SoftDeleteBranchAsync(int branchId, int deletedBy);
    
    // Queries
    Task<Branch?> GetBranchByIdAsync(int branchId);
    Task<Branch?> GetBranchByNameAsync(string name);
    Task<IEnumerable<Branch>> GetAllBranchesAsync(bool includeInactive = false);
    Task<IEnumerable<Branch>> GetActiveBranchesAsync();
    
    // Validation
    Task<bool> BranchExistsAsync(int branchId);
    Task<bool> BranchNameExistsAsync(string name, int? excludeBranchId = null);
    
    // Branch Statistics
    Task<int> GetBranchEmployeeCountAsync(int branchId);
    Task<int> GetBranchActiveJobOrdersCountAsync(int branchId);
    Task<BranchStatisticsDto> GetBranchStatisticsAsync(int branchId);
    
    // Status Management
    Task<bool> ActivateBranchAsync(int branchId);
    Task<bool> DeactivateBranchAsync(int branchId);
    
    // User Assignment
    Task<bool> AssignUserToBranchAsync(int userId, int branchId);
    Task<bool> RemoveUserFromBranchAsync(int userId);
}