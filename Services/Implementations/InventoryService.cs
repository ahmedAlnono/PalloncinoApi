using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;
using Task = System.Threading.Tasks.Task;

namespace Palloncino.Services.Implementations;

public class InventoryService(
    ApplicationDbContext context,
    ILogger<InventoryService> logger,
    IMapper mapper,
    INotificationService notificationService) : IInventoryService
{
    // ========== Reservation / Consumption ==========

    // Current business rule in this codebase:
    // - Reserving reduces on-hand quantity immediately.
    // - Consuming does not reduce again (already reserved).
    // - Returning increases on-hand quantity.
    public async Task<InventoryItem> ReserveInventoryAsync(int inventoryItemId, int quantity, string reason)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero");

        var item = await GetInventoryItemByIdAsync(inventoryItemId);
        if (item == null)
            throw new InvalidOperationException("Inventory item not found");

        if (item.Quantity < quantity)
            throw new InvalidOperationException($"Insufficient stock for item {item.Title}");

        var oldQuantity = item.Quantity;
        item.Quantity -= quantity;
        item.Status = DetermineInventoryStatus(item.Quantity, item.MinStockLevel);
        item.UpdatedAt = DateTime.UtcNow;

        context.InventoryMovements.Add(new InventoryMovement
        {
            InventoryItemId = item.Id,
            Type = MovementType.StockOut,
            Quantity = quantity,
            Reason = $"RESERVED: {reason}",
            PerformedBy = 1,
            CreatedAt = DateTime.UtcNow
        });

        logger.LogInformation(
            "Inventory Reserved: {Quantity} of {ItemTitle} (SKU: {Sku}). Reason: {Reason}. Old: {OldQty}, New: {NewQty}",
            quantity, item.Title, item.Sku, reason, oldQuantity, item.Quantity);

        await context.SaveChangesAsync();
        return item;
    }

    public async Task<InventoryItem> ConsumeInventoryAsync(int inventoryItemId, int quantity, string reason)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero");

        var item = await GetInventoryItemByIdAsync(inventoryItemId);
        if (item == null)
            throw new InvalidOperationException("Inventory item not found");

        logger.LogInformation("Inventory Consumed: {Quantity} of {ItemTitle}. Reason: {Reason}", quantity, item.Title, reason);
        return item;
    }

    public async Task<InventoryItem> ReturnToInventoryAsync(int inventoryItemId, int quantity, string reason)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero");

        var item = await GetInventoryItemByIdAsync(inventoryItemId);
        if (item == null)
            throw new InvalidOperationException("Inventory item not found");

        var oldQuantity = item.Quantity;
        item.Quantity += quantity;
        item.Status = DetermineInventoryStatus(item.Quantity, item.MinStockLevel);
        item.UpdatedAt = DateTime.UtcNow;

        context.InventoryMovements.Add(new InventoryMovement
        {
            InventoryItemId = item.Id,
            Type = MovementType.Return,
            Quantity = quantity,
            Reason = $"RETURNED: {reason}",
            PerformedBy = 1,
            CreatedAt = DateTime.UtcNow
        });

        logger.LogInformation(
            "Inventory Returned: {Quantity} of {ItemTitle} (SKU: {Sku}). Reason: {Reason}. Old: {OldQty}, New: {NewQty}",
            quantity, item.Title, item.Sku, reason, oldQuantity, item.Quantity);
        await context.SaveChangesAsync();
        return item;
    }

    // ========== Existing DTO-based APIs (kept for controllers) ==========

    public async Task<IEnumerable<InventoryItemDto>> GetInventoryItemsAsync(int page, int pageSize)
    {
        try
        {
            var items = await context.InventoryItems
            .AsNoTracking()
            .Where(i => !i.IsDeleted)
            .Skip((page - 1) * pageSize)
            .ToListAsync();
            return mapper.Map<List<InventoryItemDto>>(items);
        }
        catch (System.Exception)
        {
            logger.LogError("fail to inventory items");
            throw;
        }
    }
    public async Task<InventoryItem> CreateInventoryItemAsync(CreateInventoryItemDto dto)
    {
        try
        {
            var item = mapper.Map<InventoryItem>(dto);
            return await CreateInventoryItemAsync(item);
        }
        catch (Exception)
        {
            logger.LogError("fail to create inventory item");
            throw new Exception("item not created");
        }
    }
    public async Task<bool> UpdateInventoryItemDto(UpdateInventoryItemDto dto, int itemId)
    {
        try
        {
            var item = await context.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId && !i.IsDeleted)
            ?? throw new Exception("item not found");
            if (dto.Title is not null)
                item.Title = dto.Title;
            if (dto.PurchasePrice is not null)
                item.PurchasePrice = (decimal)dto.PurchasePrice;
            if (dto.MinStockLevel is not null)
                item.MinStockLevel = dto.MinStockLevel;
            if (dto.SalePrice is not null)
                item.SalePrice = (decimal)dto.SalePrice;
            if(dto.Category is not null)
                item.Category = dto.Category;
            item.Status = DetermineInventoryStatus(item.Quantity, item.MinStockLevel);
            await context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            logger.LogError("fail to update item with it {}",itemId);
            throw new Exception("fail to update item");
        }
    }

    // ========== CRUD Operations ==========

    public async Task<InventoryItem> CreateInventoryItemAsync(InventoryItem item)
    {
        if (await SkuExistsAsync(item.Sku))
            throw new InvalidOperationException($"SKU '{item.Sku}' already exists");

        if (item.BranchId.HasValue)
        {
            var branchExists = await context.Branches.AnyAsync(b => b.Id == item.BranchId.Value && !b.IsDeleted);
            if (!branchExists)
                throw new InvalidOperationException($"Branch with ID {item.BranchId.Value} not found");
        }

        if (item.SalePrice < 0)
            throw new InvalidOperationException("Sale price cannot be negative");

        if (item.PurchasePrice < 0)
            throw new InvalidOperationException("Purchase price cannot be negative");

        item.Status = DetermineInventoryStatus(item.Quantity, item.MinStockLevel);
        item.CreatedAt = DateTime.UtcNow;
        item.IsActive = true;

        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Inventory item created: {Title} (SKU: {Sku}) with quantity {Quantity}",
            item.Title, item.Sku, item.Quantity);

        return item;
    }

    public async Task<InventoryItem> UpdateInventoryItemAsync(InventoryItem item)
    {
        var existingItem = await GetInventoryItemByIdAsync(item.Id);
        if (existingItem == null)
            throw new InvalidOperationException($"Inventory item with ID {item.Id} not found");

        if (item.Sku != existingItem.Sku && await SkuExistsAsync(item.Sku, item.Id))
            throw new InvalidOperationException($"SKU '{item.Sku}' already exists");

        existingItem.Title = item.Title;
        existingItem.Sku = item.Sku;
        existingItem.PurchasePrice = item.PurchasePrice;
        existingItem.SalePrice = item.SalePrice;
        existingItem.Unit = item.Unit;
        existingItem.BranchId = item.BranchId;
        existingItem.MinStockLevel = item.MinStockLevel;
        existingItem.Category = item.Category;
        existingItem.UpdatedAt = DateTime.UtcNow;
        existingItem.UpdatedBy = item.UpdatedBy;
        existingItem.Status = DetermineInventoryStatus(existingItem.Quantity, existingItem.MinStockLevel);

        await context.SaveChangesAsync();

        logger.LogInformation("Inventory item updated: {Title} (SKU: {Sku})", existingItem.Title, existingItem.Sku);

        return existingItem;
    }

    public async Task<bool> DeleteInventoryItemAsync(int itemId)
    {
        var item = await GetInventoryItemByIdAsync(itemId);
        if (item == null)
            return false;

        var isUsed = await context.JobOrderItems
            .AnyAsync(i => i.InventoryItemId == itemId && i.Status != JobOrderItemStatus.Cancelled);

        if (isUsed)
            throw new InvalidOperationException("Cannot delete inventory item that is used in active job orders");

        context.InventoryItems.Remove(item);
        await context.SaveChangesAsync();

        logger.LogWarning("Inventory item permanently deleted: {Title} (SKU: {Sku})", item.Title, item.Sku);
        return true;
    }

    public async Task<bool> SoftDeleteInventoryItemAsync(int itemId, int deletedBy)
    {
        var item = await GetInventoryItemByIdAsync(itemId);
        if (item == null)
            return false;

        item.SoftDelete(deletedBy);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Inventory item soft deleted: {Title} (SKU: {Sku}) by User {DeletedBy}",
            item.Title, item.Sku, deletedBy);

        return true;
    }

    // ========== Queries ==========

    public async Task<InventoryItem?> GetInventoryItemByIdAsync(int itemId)
    {
        return await context.InventoryItems
            .Include(i => i.Branch)
            .FirstOrDefaultAsync(i => i.Id == itemId && !i.IsDeleted);
    }

    public async Task<InventoryItem?> GetInventoryItemBySkuAsync(string sku)
    {
        return await context.InventoryItems
            .Include(i => i.Branch)
            .FirstOrDefaultAsync(i => i.Sku == sku && !i.IsDeleted);
    }

    public async Task<IEnumerable<InventoryItem>> GetAllInventoryItemsAsync(InventoryFilter? filter = null)
    {
        var query = context.InventoryItems
            .Include(i => i.Branch)
            .Where(i => !i.IsDeleted);

        if (filter != null)
        {
            if (filter.BranchId.HasValue)
                query = query.Where(i => i.BranchId == filter.BranchId.Value);

            if (!string.IsNullOrEmpty(filter.Category))
                query = query.Where(i => i.Category == filter.Category);

            if (filter.Status.HasValue)
                query = query.Where(i => i.Status == filter.Status.Value);

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                var search = filter.SearchTerm.ToLower();
                query = query.Where(i => i.Title.ToLower().Contains(search) || i.Sku.ToLower().Contains(search));
            }

            if (!filter.IncludeOutOfStock)
                query = query.Where(i => i.Quantity > 0);

            if (!filter.IncludeLowStock && filter.IncludeOutOfStock)
                query = query.Where(i => !i.MinStockLevel.HasValue || i.Quantity > i.MinStockLevel.Value);
        }

        return await query.OrderBy(i => i.Title).ToListAsync();
    }

    public async Task<IEnumerable<InventoryItem>> GetInventoryItemsByBranchAsync(int branchId)
    {
        return await context.InventoryItems
            .Where(i => i.BranchId == branchId && !i.IsDeleted)
            .OrderBy(i => i.Title)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync(int branchId)
    {
        return await context.InventoryItems
            .Where(i => i.BranchId == branchId
                        && !i.IsDeleted
                        && i.MinStockLevel.HasValue
                        && i.Quantity <= i.MinStockLevel.Value
                        && i.Quantity > 0)
            .OrderBy(i => i.Quantity)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryItem>> GetOutOfStockItemsAsync(int branchId)
    {
        return await context.InventoryItems
            .Where(i => i.BranchId == branchId && !i.IsDeleted && i.Quantity <= 0)
            .OrderBy(i => i.Title)
            .ToListAsync();
    }

    // ========== Stock Management ==========

    public async Task<InventoryItem> AddStockAsync(int itemId, int quantity, string reason, int performedBy)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero");

        var item = await GetInventoryItemByIdAsync(itemId);
        if (item == null)
            throw new InvalidOperationException($"Inventory item with ID {itemId} not found");

        var oldQuantity = item.Quantity;
        item.Quantity += quantity;
        item.Status = DetermineInventoryStatus(item.Quantity, item.MinStockLevel);
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = performedBy;

        context.InventoryMovements.Add(new InventoryMovement
        {
            InventoryItemId = itemId,
            Type = MovementType.StockIn,
            Quantity = quantity,
            Reason = reason,
            PerformedBy = performedBy,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        logger.LogInformation(
            "Stock added: {Quantity} units of {ItemTitle} (SKU: {Sku}). Reason: {Reason}. Old: {OldQty}, New: {NewQty}",
            quantity, item.Title, item.Sku, reason, oldQuantity, item.Quantity);

        return item;
    }

    public async Task<InventoryItem> RemoveStockAsync(int itemId, int quantity, string reason, int performedBy, int? relatedJobOrderId = null)
    {
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero");

        var item = await GetInventoryItemByIdAsync(itemId);
        if (item == null)
            throw new InvalidOperationException($"Inventory item with ID {itemId} not found");

        if (item.Quantity < quantity)
            throw new InvalidOperationException($"Insufficient stock. Available: {item.Quantity}, Requested: {quantity}");

        var oldQuantity = item.Quantity;
        item.Quantity -= quantity;
        item.Status = DetermineInventoryStatus(item.Quantity, item.MinStockLevel);
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = performedBy;

        context.InventoryMovements.Add(new InventoryMovement
        {
            InventoryItemId = itemId,
            Type = MovementType.StockOut,
            Quantity = quantity,
            Reason = reason,
            PerformedBy = performedBy,
            RelatedJobOrderId = relatedJobOrderId,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        logger.LogInformation(
            "Stock removed: {Quantity} units of {ItemTitle} (SKU: {Sku}). Reason: {Reason}. Old: {OldQty}, New: {NewQty}",
            quantity, item.Title, item.Sku, reason, oldQuantity, item.Quantity);

        if (item.MinStockLevel.HasValue && item.Quantity <= item.MinStockLevel.Value)
            await notificationService.SendLowStockNotification(item.Id, item.Quantity);

        return item;
    }

    public async Task<InventoryItem> TransferStockAsync(int itemId, int fromBranchId, int toBranchId, int quantity, int transferredBy)
    {
        if (fromBranchId == toBranchId)
            throw new InvalidOperationException("Source and destination branches cannot be the same");

        var item = await GetInventoryItemByIdAsync(itemId);
        if (item == null)
            throw new InvalidOperationException($"Inventory item with ID {itemId} not found");

        if (item.BranchId != fromBranchId)
            throw new InvalidOperationException($"Item is not located at branch {fromBranchId}");

        if (item.Quantity < quantity)
            throw new InvalidOperationException($"Insufficient stock at source branch. Available: {item.Quantity}, Requested: {quantity}");

        item.Quantity -= quantity;

        var destItem = await context.InventoryItems
            .FirstOrDefaultAsync(i => i.Sku == item.Sku && i.BranchId == toBranchId && !i.IsDeleted);

        if (destItem == null)
        {
            destItem = new InventoryItem
            {
                Title = item.Title,
                Sku = item.Sku,
                PurchasePrice = item.PurchasePrice,
                SalePrice = item.SalePrice,
                Quantity = quantity,
                Unit = item.Unit,
                BranchId = toBranchId,
                MinStockLevel = item.MinStockLevel,
                Category = item.Category,
                Status = DetermineInventoryStatus(quantity, item.MinStockLevel),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = transferredBy,
                IsActive = true
            };
            context.InventoryItems.Add(destItem);
        }
        else
        {
            destItem.Quantity += quantity;
            destItem.Status = DetermineInventoryStatus(destItem.Quantity, destItem.MinStockLevel);
            destItem.UpdatedAt = DateTime.UtcNow;
            destItem.UpdatedBy = transferredBy;
        }

        item.Status = DetermineInventoryStatus(item.Quantity, item.MinStockLevel);
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = transferredBy;

        var sourceMovement = new InventoryMovement
        {
            InventoryItemId = itemId,
            Type = MovementType.Transfer,
            Quantity = -quantity,
            Reason = $"Transferred to branch {toBranchId}",
            PerformedBy = transferredBy,
            CreatedAt = DateTime.UtcNow
        };

        var destMovement = new InventoryMovement
        {
            InventoryItem = destItem,
            Type = MovementType.Transfer,
            Quantity = quantity,
            Reason = $"Transferred from branch {fromBranchId}",
            PerformedBy = transferredBy,
            CreatedAt = DateTime.UtcNow
        };

        context.InventoryMovements.Add(sourceMovement);
        context.InventoryMovements.Add(destMovement);

        await context.SaveChangesAsync();

        logger.LogInformation(
            "Stock transferred: {Quantity} units of {ItemTitle} (SKU: {Sku}) from Branch {FromBranch} to Branch {ToBranch}",
            quantity, item.Title, item.Sku, fromBranchId, toBranchId);

        return item;
    }

    // ========== Movement History ==========

    public async Task<IEnumerable<InventoryMovement>> GetInventoryMovementsAsync(int itemId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = context.InventoryMovements
            .Include(m => m.Performer)
            .Include(m => m.JobOrder)
            .Where(m => m.InventoryItemId == itemId);

        if (fromDate.HasValue)
            query = query.Where(m => m.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(m => m.CreatedAt <= toDate.Value);

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<InventoryMovement>> GetInventoryMovementsByJobOrderAsync(int jobOrderId)
    {
        return await context.InventoryMovements
            .Include(m => m.InventoryItem)
            .Include(m => m.Performer)
            .Where(m => m.RelatedJobOrderId == jobOrderId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<InventoryMovement?> GetLastMovementAsync(int itemId)
    {
        return await context.InventoryMovements
            .Where(m => m.InventoryItemId == itemId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();
    }

    // ========== Validation ==========

    public async Task<bool> InventoryItemExistsAsync(int itemId)
    {
        return await context.InventoryItems.AnyAsync(i => i.Id == itemId && !i.IsDeleted);
    }

    public async Task<bool> SkuExistsAsync(string sku, int? excludeItemId = null)
    {
        var query = context.InventoryItems.Where(i => i.Sku == sku && !i.IsDeleted);
        if (excludeItemId.HasValue)
            query = query.Where(i => i.Id != excludeItemId.Value);
        return await query.AnyAsync();
    }

    public async Task<bool> HasSufficientStockAsync(int itemId, int quantity)
    {
        var item = await GetInventoryItemByIdAsync(itemId);
        return item != null && item.Quantity >= quantity;
    }

    public async Task<int> GetAvailableStockAsync(int itemId)
    {
        var item = await GetInventoryItemByIdAsync(itemId);
        return item?.Quantity ?? 0;
    }

    // ========== Business Logic ==========

    public async Task<decimal> GetTotalInventoryValueAsync(int? branchId = null)
    {
        var query = context.InventoryItems.Where(i => !i.IsDeleted && i.Quantity > 0);
        if (branchId.HasValue)
            query = query.Where(i => i.BranchId == branchId.Value);
        var items = await query.ToListAsync();
        return items.Sum(i => i.Quantity * i.PurchasePrice);
    }

    public async Task<InventoryStatisticsDto> GetInventoryStatisticsAsync(int? branchId = null)
    {
        var query = context.InventoryItems.Where(i => !i.IsDeleted);
        if (branchId.HasValue)
            query = query.Where(i => i.BranchId == branchId.Value);
        var items = await query.ToListAsync();

        return new InventoryStatisticsDto
        {
            TotalItems = items.Count,
            TotalQuantity = items.Sum(i => i.Quantity),
            TotalValue = items.Sum(i => i.Quantity * i.PurchasePrice),
            LowStockItems = items.Count(i => i.MinStockLevel.HasValue && i.Quantity <= i.MinStockLevel.Value && i.Quantity > 0),
            OutOfStockItems = items.Count(i => i.Quantity <= 0),
            ActiveItems = items.Count(i => i.Status == InventoryStatus.InStock),
            ItemsByCategory = items.GroupBy(i => i.Category ?? "Uncategorized").ToDictionary(g => g.Key, g => g.Count()),
            ValueByCategory = items.GroupBy(i => i.Category ?? "Uncategorized")
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity * i.PurchasePrice))
        };
    }

    public async Task<InventoryReportDto> GenerateInventoryReportAsync(int? branchId = null)
    {
        var branch = branchId.HasValue
            ? await context.Branches.FirstOrDefaultAsync(b => b.Id == branchId.Value)
            : null;

        var items = await GetAllInventoryItemsAsync(new InventoryFilter
        {
            BranchId = branchId,
            IncludeOutOfStock = true,
            IncludeLowStock = true
        });

        var statistics = await GetInventoryStatisticsAsync(branchId);
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var reportItems = new List<InventoryItemReport>();
        foreach (var item in items)
        {
            var movementsThisMonth = await context.InventoryMovements
                .CountAsync(m => m.InventoryItemId == item.Id && m.CreatedAt >= startOfMonth);

            reportItems.Add(new InventoryItemReport
            {
                Id = item.Id,
                Title = item.Title,
                Sku = item.Sku,
                Category = item.Category ?? "Uncategorized",
                CurrentStock = item.Quantity,
                MinStockLevel = item.MinStockLevel ?? 0,
                Status = item.Status.ToString(),
                PurchasePrice = item.PurchasePrice,
                SalePrice = item.SalePrice,
                StockValue = item.Quantity * item.PurchasePrice,
                MovementsThisMonth = movementsThisMonth
            });
        }

        return new InventoryReportDto
        {
            GeneratedAt = DateTime.UtcNow,
            BranchId = branchId,
            BranchName = branch?.Name,
            Items = reportItems.OrderBy(i => i.Title).ToList(),
            Summary = statistics
        };
    }

    // ========== Bulk Operations ==========

    public async Task<int> BulkUpdateStockLevelsAsync(List<StockUpdateDto> updates, int performedBy)
    {
        var updatedCount = 0;

        foreach (var update in updates)
        {
            var item = await GetInventoryItemByIdAsync(update.InventoryItemId);
            if (item == null)
                continue;

            var newQuantity = item.Quantity + update.QuantityChange;
            if (newQuantity < 0)
                throw new InvalidOperationException(
                    $"Insufficient stock for item {item.Title}. Available: {item.Quantity}, Change: {update.QuantityChange}");

            item.Quantity = newQuantity;
            item.Status = DetermineInventoryStatus(item.Quantity, item.MinStockLevel);
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = performedBy;

            context.InventoryMovements.Add(new InventoryMovement
            {
                InventoryItemId = item.Id,
                Type = update.QuantityChange > 0 ? MovementType.StockIn : MovementType.StockOut,
                Quantity = Math.Abs(update.QuantityChange),
                Reason = update.Reason,
                PerformedBy = performedBy,
                CreatedAt = DateTime.UtcNow
            });

            updatedCount++;
        }

        await context.SaveChangesAsync();

        logger.LogInformation("Bulk stock update completed: {UpdatedCount} items updated by User {PerformedBy}", updatedCount, performedBy);
        return updatedCount;
    }

    public async Task<int> BulkAdjustPricesAsync(decimal percentageIncrease, string? category = null)
    {
        if (percentageIncrease <= -100)
            throw new InvalidOperationException("Percentage increase cannot be less than -100%");

        var query = context.InventoryItems.Where(i => !i.IsDeleted);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(i => i.Category != null && i.Category == category);

        var items = await query.ToListAsync();
        foreach (var item in items)
        {
            item.PurchasePrice = item.PurchasePrice * (1 + percentageIncrease / 100);
            item.SalePrice = item.SalePrice * (1 + percentageIncrease / 100);
            item.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();

        logger.LogInformation("Bulk price adjustment: {PercentageIncrease}% applied to {Count} items", percentageIncrease, items.Count);
        return items.Count;
    }

    // ========== Alerts & Notifications ==========

    public async Task CheckLowStockAlertsAsync()
    {
        var lowStockItems = await context.InventoryItems
            .Where(i => !i.IsDeleted
                        && i.MinStockLevel.HasValue
                        && i.Quantity <= i.MinStockLevel.Value
                        && i.Quantity > 0)
            .ToListAsync();

        foreach (var item in lowStockItems)
            await notificationService.SendLowStockNotification(item.Id, item.Quantity);

        if (lowStockItems.Any())
            logger.LogWarning("Low stock alert: {Count} items are below minimum stock level", lowStockItems.Count);
    }

    public async Task<IEnumerable<InventoryItem>> GetItemsNeedingReorderAsync(int branchId)
    {
        return await context.InventoryItems
            .Where(i => i.BranchId == branchId
                        && !i.IsDeleted
                        && i.MinStockLevel.HasValue
                        && i.Quantity <= i.MinStockLevel.Value)
            .OrderBy(i => i.Quantity)
            .ToListAsync();
    }

    // ========== Private Helper Methods ==========

    private static InventoryStatus DetermineInventoryStatus(int quantity, int? minStockLevel)
    {
        if (quantity <= 0)
            return InventoryStatus.OutOfStock;

        if (minStockLevel.HasValue && quantity <= minStockLevel.Value)
            return InventoryStatus.LowStock;

        return InventoryStatus.InStock;
    }
}
