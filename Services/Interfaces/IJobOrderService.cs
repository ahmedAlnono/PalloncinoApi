using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;

namespace Palloncino.Services.Interfaces;

public interface IJobOrderService
{
    // ========== CRUD Operations ==========
    Task<JobOrder> CreateJobOrderAsync(JobOrder jobOrder, List<JobOrderItem>? items = null);
    Task<JobOrder> UpdateJobOrderAsync(JobOrder jobOrder);
    Task<bool> DeleteJobOrderAsync(int jobOrderId);
    Task<bool> SoftDeleteJobOrderAsync(int jobOrderId, int deletedBy);
    
    // ========== Queries ==========
    Task<JobOrder?> GetJobOrderByIdAsync(int jobOrderId);
    Task<JobOrder?> GetJobOrderByNumberAsync(string jobNumber);
    Task<IEnumerable<JobOrder>> GetAllJobOrdersAsync(JobOrderFilter? filter = null);
    Task<IEnumerable<JobOrder>> GetJobOrdersByBranchAsync(int branchId, JobOrderStatus? status = null);
    Task<IEnumerable<JobOrder>> GetJobOrdersByCustomerAsync(int customerId);
    Task<IEnumerable<JobOrder>> GetJobOrdersByDriverAsync(int driverId);
    Task<IEnumerable<JobOrder>> GetJobOrdersByDesignerAsync(int designerId);
    
    // ========== Status Management ==========
    Task<JobOrder> UpdateJobOrderStatusAsync(int jobOrderId, JobOrderStatus status, int updatedBy, string? reason = null);
    Task<bool> SkipReturnPhaseAsync(int jobOrderId, int skippedBy, string reason);
    Task<bool> CompleteJobOrderAsync(int jobOrderId, int completedBy);
    Task<bool> CancelJobOrderAsync(int jobOrderId, int cancelledBy, string reason);
    
    // ========== Job Order Items Management ==========
    Task<JobOrderItem> AddJobOrderItemAsync(int jobOrderId, int inventoryItemId, int quantity, decimal? sellingPricePerUnit = null);
    Task<bool> RemoveJobOrderItemAsync(int jobOrderItemId);
    Task<JobOrderItem> UpdateJobOrderItemQuantityAsync(int jobOrderItemId, int quantity);
    Task<JobOrderItem> MarkItemAsPreparedAsync(int jobOrderItemId, int preparedBy);
    Task<JobOrderItem> MarkItemAsDeliveredAsync(int jobOrderItemId, int deliveredBy);
    Task<JobOrderItem> ReturnRentalItemAsync(int jobOrderItemId, int returnedToId, ReturnCondition condition, decimal? damageDeduction = null, string? deductionReason = null, string? proofImageUrl = null);
    Task<IEnumerable<JobOrderItem>> GetJobOrderItemsAsync(int jobOrderId);
    
    // ========== Task Management ==========
    Task<IEnumerable<Models.Entities.Task>> GetJobOrderTasksAsync(int jobOrderId);
    Task<JobOrder> AutoGenerateTasksAsync(int jobOrderId);
    
    // ========== Validation ==========
    Task<bool> JobOrderExistsAsync(int jobOrderId);
    Task<bool> JobOrderNumberExistsAsync(string jobNumber, int? excludeJobOrderId = null);
    
    // ========== Business Logic ==========
    Task<decimal> CalculateJobOrderTotalCostAsync(int jobOrderId);
    Task<decimal> CalculateJobOrderProfitAsync(int jobOrderId);
    Task<JobOrder> RecalculateJobOrderCostsAsync(int jobOrderId);
    
    // ========== Dashboard & Counters ==========
    Task<IEnumerable<JobOrder>> GetUpcomingJobOrdersAsync(int branchId, int daysAhead = 7);
    Task<IEnumerable<JobOrder>> GetOverdueJobOrdersAsync(int branchId);
    Task<JobOrderDashboardDto> GetJobOrderDashboardAsync(int branchId);
    
    // ========== Driver Specific ==========
    Task<IEnumerable<JobOrder>> GetDriverDeliveriesAsync(int driverId, DateTime? date = null);
    Task<DeliveryChecklistDto> GetDeliveryChecklistAsync(int jobOrderId);
    Task<bool> UpdateDeliveryChecklistAsync(int jobOrderId, int driverId, List<ChecklistUpdateDto> checklistUpdates);
    
    // ========== Designer Specific ==========
    Task<IEnumerable<JobOrder>> GetDesignerJobOrdersAsync(int designerId, Models.Enums.TaskStatus? status = null);
    
    // ========== Reports ==========
    Task<JobOrderReportDto> GetJobOrderReportAsync(DateTime startDate, DateTime endDate, int? branchId = null);
}