using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
namespace Palloncino.Services.Interfaces;

public interface IInventoryService
{
    // Reservation / consumption
    System.Threading.Tasks.Task<InventoryItem> ReserveInventoryAsync(int inventoryItemId, int quantity, string reason);
    System.Threading.Tasks.Task<InventoryItem> ConsumeInventoryAsync(int inventoryItemId, int quantity, string reason);
    System.Threading.Tasks.Task<InventoryItem> ReturnToInventoryAsync(int inventoryItemId, int quantity, string reason);

    // Existing controller DTO methods (kept)
    System.Threading.Tasks.Task<IEnumerable<InventoryItemDto>> GetInventoryItemsAsync(int page, int pageSize);
    System.Threading.Tasks.Task<InventoryItem> CreateInventoryItemAsync(CreateInventoryItemDto dto);
    System.Threading.Tasks.Task<bool> UpdateInventoryItemDto(UpdateInventoryItemDto dto, int itemId);

    // CRUD
    System.Threading.Tasks.Task<InventoryItem> CreateInventoryItemAsync(InventoryItem item);
    System.Threading.Tasks.Task<InventoryItem> UpdateInventoryItemAsync(InventoryItem item);
    System.Threading.Tasks.Task<bool> DeleteInventoryItemAsync(int itemId);
    System.Threading.Tasks.Task<bool> SoftDeleteInventoryItemAsync(int itemId, int deletedBy);

    // Queries
    System.Threading.Tasks.Task<InventoryItem?> GetInventoryItemByIdAsync(int itemId);
    System.Threading.Tasks.Task<InventoryItem?> GetInventoryItemBySkuAsync(string sku);
    System.Threading.Tasks.Task<IEnumerable<InventoryItem>> GetAllInventoryItemsAsync(InventoryFilter? filter = null);
    System.Threading.Tasks.Task<IEnumerable<InventoryItem>> GetInventoryItemsByBranchAsync(int branchId);
    System.Threading.Tasks.Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync(int branchId);
    System.Threading.Tasks.Task<IEnumerable<InventoryItem>> GetOutOfStockItemsAsync(int branchId);

    // Stock management
    System.Threading.Tasks.Task<InventoryItem> AddStockAsync(int itemId, int quantity, string reason, int performedBy);
    System.Threading.Tasks.Task<InventoryItem> RemoveStockAsync(int itemId, int quantity, string reason, int performedBy, int? relatedJobOrderId = null);
    System.Threading.Tasks.Task<InventoryItem> TransferStockAsync(int itemId, int fromBranchId, int toBranchId, int quantity, int transferredBy);

    // Movements
    System.Threading.Tasks.Task<IEnumerable<InventoryMovement>> GetInventoryMovementsAsync(int itemId, DateTime? fromDate = null, DateTime? toDate = null);
    System.Threading.Tasks.Task<IEnumerable<InventoryMovement>> GetInventoryMovementsByJobOrderAsync(int jobOrderId);
    System.Threading.Tasks.Task<InventoryMovement?> GetLastMovementAsync(int itemId);

    // Validation
    System.Threading.Tasks.Task<bool> InventoryItemExistsAsync(int itemId);
    System.Threading.Tasks.Task<bool> SkuExistsAsync(string sku, int? excludeItemId = null);
    System.Threading.Tasks.Task<bool> HasSufficientStockAsync(int itemId, int quantity);
    System.Threading.Tasks.Task<int> GetAvailableStockAsync(int itemId);

    // Business / reports
    System.Threading.Tasks.Task<decimal> GetTotalInventoryValueAsync(int? branchId = null);
    System.Threading.Tasks.Task<InventoryStatisticsDto> GetInventoryStatisticsAsync(int? branchId = null);
    System.Threading.Tasks.Task<InventoryReportDto> GenerateInventoryReportAsync(int? branchId = null);

    // Bulk
    System.Threading.Tasks.Task<int> BulkUpdateStockLevelsAsync(List<StockUpdateDto> updates, int performedBy);
    System.Threading.Tasks.Task<int> BulkAdjustPricesAsync(decimal percentageIncrease, string? category = null);

    // Alerts
    System.Threading.Tasks.Task CheckLowStockAlertsAsync();
    System.Threading.Tasks.Task<IEnumerable<InventoryItem>> GetItemsNeedingReorderAsync(int branchId);
}
