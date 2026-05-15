using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;
using TaskStatus = Palloncino.Models.Enums.TaskStatus;

namespace Palloncino.Services.Implementations;

public class JobOrderService(
    ApplicationDbContext context,
    ILogger<JobOrderService> logger,
    IInventoryService inventoryService) : IJobOrderService
{
    // ========== CRUD Operations ==========
    
    public async Task<JobOrder> CreateJobOrderAsync(JobOrder jobOrder, List<JobOrderItem>? items = null)
    {
        // Generate unique job number
        jobOrder.JobNumber = await GenerateJobNumberAsync();
        
        // Validate branch exists
        var branchExists = await context.Branches.AnyAsync(b => b.Id == jobOrder.BranchId && !b.IsDeleted);
        if (!branchExists)
            throw new InvalidOperationException($"Branch with ID {jobOrder.BranchId} not found");
        
        // Validate coordinator if assigned
        if (jobOrder.AssignedToCoordinator.HasValue)
        {
            var coordinator = await context.Users
                .FirstOrDefaultAsync(u => u.Id == jobOrder.AssignedToCoordinator.Value 
                    && (u.Role == UserRole.Employee || u.Role == UserRole.Admin)
                    && !u.IsDeleted);
            
            if (coordinator == null)
                throw new InvalidOperationException($"Coordinator with ID {jobOrder.AssignedToCoordinator.Value} not found");
        }
        
        // Validate due date
        if (jobOrder.DueAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Due date must be in the future");
        
        jobOrder.Status = JobOrderStatus.Pending;
        jobOrder.CreatedAt = DateTime.UtcNow;
        jobOrder.IsActive = true;
        
        context.JobOrders.Add(jobOrder);
        await context.SaveChangesAsync();
        
        // Add items if provided
        if (items != null && items.Any())
        {
            foreach (var item in items)
            {
                item.JobOrderId = jobOrder.Id;
                item.CreatedAt = DateTime.UtcNow;
                item.Status = JobOrderItemStatus.Pending;
                context.JobOrderItems.Add(item);
            }
            
            await context.SaveChangesAsync();
            await RecalculateJobOrderCostsAsync(jobOrder.Id);
        }
        
        // Auto-generate tasks based on execution type
        await AutoGenerateTasksAsync(jobOrder.Id);
        
        logger.LogInformation("JobOrder created: {JobNumber} (ID: {JobOrderId}) with type {ExecutionType}", 
            jobOrder.JobNumber, jobOrder.Id, jobOrder.ExecutionType);
        
        return jobOrder;
    }
    
    public async Task<JobOrder> UpdateJobOrderAsync(JobOrder jobOrder)
    {
        var existing = await GetJobOrderByIdAsync(jobOrder.Id);
        if (existing == null)
            throw new InvalidOperationException($"JobOrder with ID {jobOrder.Id} not found");
        
        // Check if can update (not completed or cancelled)
        if (existing.Status == JobOrderStatus.Completed || existing.Status == JobOrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot update {existing.Status} job order");
        
        // Update fields
        existing.ExecutionType = jobOrder.ExecutionType;
        existing.DueAt = jobOrder.DueAt;
        existing.AssignedToCoordinator = jobOrder.AssignedToCoordinator;
        existing.SpecialInstructions = jobOrder.SpecialInstructions;
        existing.DeliveryAddress = jobOrder.DeliveryAddress;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = jobOrder.UpdatedBy;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("JobOrder updated: {JobNumber} (ID: {JobOrderId})", existing.JobNumber, existing.Id);
        
        return existing;
    }
    
    public async Task<bool> DeleteJobOrderAsync(int jobOrderId)
    {
        var jobOrder = await GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            return false;
        
        // Cannot delete if has tasks in progress
        var hasActiveTasks = await context.Tasks
            .AnyAsync(t => t.JobOrderId == jobOrderId && t.Status != Models.Enums.TaskStatus.Completed);
        
        if (hasActiveTasks)
            throw new InvalidOperationException("Cannot delete job order with active tasks");
        
        // Remove all related entities
        var taskIds = await context.Tasks
            .Where(t => t.JobOrderId == jobOrderId)
            .Select(t => t.Id)
            .ToListAsync();
        
        foreach (var taskId in taskIds)
        {
            var subTasks = context.SubTasks.Where(st => st.TaskId == taskId);
            context.SubTasks.RemoveRange(subTasks);
            
            var checklistItems = context.ChecklistItems.Where(ci => ci.TaskId == taskId);
            context.ChecklistItems.RemoveRange(checklistItems);
        }
        
        var tasks = context.Tasks.Where(t => t.JobOrderId == jobOrderId);
        context.Tasks.RemoveRange(tasks);
        
        var items = context.JobOrderItems.Where(i => i.JobOrderId == jobOrderId);
        context.JobOrderItems.RemoveRange(items);
        
        context.JobOrders.Remove(jobOrder);
        await context.SaveChangesAsync();
        
        logger.LogWarning("JobOrder permanently deleted: {JobNumber} (ID: {JobOrderId})", jobOrder.JobNumber, jobOrderId);
        
        return true;
    }
    
    public async Task<bool> SoftDeleteJobOrderAsync(int jobOrderId, int deletedBy)
    {
        var jobOrder = await GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            return false;
        
        jobOrder.SoftDelete(deletedBy);
        await context.SaveChangesAsync();
        
        logger.LogInformation("JobOrder soft deleted: {JobNumber} (ID: {JobOrderId}) by User {DeletedBy}", 
            jobOrder.JobNumber, jobOrderId, deletedBy);
        
        return true;
    }
    
    // ========== Queries ==========
    
    public async Task<JobOrder?> GetJobOrderByIdAsync(int jobOrderId)
    {
        return await context.JobOrders
            .Include(j => j.Branch)
            .Include(j => j.Coordinator)
            .Include(j => j.SourceOrder)
                .ThenInclude(o => o != null ? o.Customer : null)
            .Include(j => j.Tasks)
            .Include(j => j.JobOrderItems)
                .ThenInclude(i => i.InventoryItem)
            .FirstOrDefaultAsync(j => j.Id == jobOrderId && !j.IsDeleted);
    }
    
    public async Task<JobOrder?> GetJobOrderByNumberAsync(string jobNumber)
    {
        return await context.JobOrders
            .Include(j => j.Branch)
            .Include(j => j.Coordinator)
            .Include(j => j.Tasks)
            .Include(j => j.JobOrderItems)
            .FirstOrDefaultAsync(j => j.JobNumber == jobNumber && !j.IsDeleted);
    }
    
    public async Task<IEnumerable<JobOrder>> GetAllJobOrdersAsync(JobOrderFilter? filter = null)
    {
        var query = context.JobOrders
            .Include(j => j.Branch)
            .Include(j => j.Coordinator)
            .Include(j => j.Tasks)
            .Where(j => !j.IsDeleted);
        
        if (filter != null)
        {
            if (filter.Status.HasValue)
                query = query.Where(j => j.Status == filter.Status.Value);
            
            if (filter.ExecutionType.HasValue)
                query = query.Where(j => j.ExecutionType == filter.ExecutionType.Value);
            
            if (filter.BranchId.HasValue)
                query = query.Where(j => j.BranchId == filter.BranchId.Value);
            
            if (filter.AssignedToCoordinator.HasValue)
                query = query.Where(j => j.AssignedToCoordinator == filter.AssignedToCoordinator.Value);
            
            if (filter.FromDate.HasValue)
                query = query.Where(j => j.CreatedAt >= filter.FromDate.Value);
            
            if (filter.ToDate.HasValue)
                query = query.Where(j => j.CreatedAt <= filter.ToDate.Value);
            
            if (!filter.IncludeCompleted)
                query = query.Where(j => j.Status != JobOrderStatus.Completed);
        }
        
        // Sort by due date (BR-09: closest first with countdown)
        return await query
            .OrderBy(j => j.DueAt)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<JobOrder>> GetJobOrdersByBranchAsync(int branchId, JobOrderStatus? status = null)
    {
        var query = context.JobOrders
            .Include(j => j.Branch)
            .Include(j => j.Coordinator)
            .Where(j => j.BranchId == branchId && !j.IsDeleted);
        
        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);
        
        return await query
            .OrderBy(j => j.DueAt)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<JobOrder>> GetJobOrdersByCustomerAsync(int customerId)
    {
        return await context.JobOrders
            .Include(j => j.Branch)
            .Include(j => j.Tasks)
            .Where(j => j.SourceOrder != null && j.SourceOrder.CustomerId == customerId && !j.IsDeleted)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<JobOrder>> GetJobOrdersByDriverAsync(int driverId)
    {
        var driverTasks = await context.Tasks
            .Where(t => t.Type == TaskType.Delivery && t.AssignedTo == driverId && !t.IsDeleted)
            .Select(t => t.JobOrderId)
            .Distinct()
            .ToListAsync();
        
        return await context.JobOrders
            .Include(j => j.Branch)
            .Include(j => j.Tasks)
            .Where(j => driverTasks.Contains(j.Id) && !j.IsDeleted)
            .OrderBy(j => j.DueAt)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<JobOrder>> GetJobOrdersByDesignerAsync(int designerId)
    {
        var designerTasks = await context.Tasks
            .Where(t => t.Type == TaskType.Design && t.AssignedTo == designerId && !t.IsDeleted)
            .Select(t => t.JobOrderId)
            .Distinct()
            .ToListAsync();
        
        return await context.JobOrders
            .Include(j => j.Branch)
            .Include(j => j.Tasks)
            .Where(j => designerTasks.Contains(j.Id) && !j.IsDeleted)
            .OrderBy(j => j.DueAt)
            .ToListAsync();
    }
    
    // ========== Status Management ==========
    
    public async Task<JobOrder> UpdateJobOrderStatusAsync(int jobOrderId, JobOrderStatus status, int updatedBy, string? reason = null)
    {
        var jobOrder = await GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            throw new InvalidOperationException($"JobOrder with ID {jobOrderId} not found");
        
        // Validate status transition
        if (!IsValidStatusTransition(jobOrder.Status, status))
            throw new InvalidOperationException($"Invalid status transition from {jobOrder.Status} to {status}");
        
        var oldStatus = jobOrder.Status;
        jobOrder.Status = status;
        jobOrder.UpdatedAt = DateTime.UtcNow;
        jobOrder.UpdatedBy = updatedBy;
        
        if (status == JobOrderStatus.Completed)
            jobOrder.ActualDeliveryAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        // Log status change in activity log (FR-SEC-02)
        logger.LogInformation("JobOrder {JobNumber} status changed from {OldStatus} to {NewStatus} by User {UpdatedBy}. Reason: {Reason}", 
            jobOrder.JobNumber, oldStatus, status, updatedBy, reason ?? "N/A");
        
        return jobOrder;
    }
    
    public async Task<bool> SkipReturnPhaseAsync(int jobOrderId, int skippedBy, string reason)
    {
        var jobOrder = await GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            return false;
        
        // Check if has rental items waiting for return
        var hasRentalItems = await context.JobOrderItems
            .AnyAsync(i => i.JobOrderId == jobOrderId && i.IsRental && i.Status != JobOrderItemStatus.Returned);
        
        if (!hasRentalItems)
            throw new InvalidOperationException("No rental items pending return");
        
        // Mark all rental items as lost and apply full deduction
        var rentalItems = await context.JobOrderItems
            .Where(i => i.JobOrderId == jobOrderId && i.IsRental && i.Status != JobOrderItemStatus.Returned)
            .ToListAsync();
        
        foreach (var item in rentalItems)
        {
            item.Status = JobOrderItemStatus.Lost;
            item.DamageDeduction = item.TotalSellingPrice ?? item.TotalCost;
            item.DeductionReason = $"Skip return: {reason}";
            item.ReturnedAt = DateTime.UtcNow;
            item.ReturnedToId = skippedBy;
        }
        
        await context.SaveChangesAsync();
        
        // Skip all return checklist items
        var returnTasks = await context.Tasks
            .Where(t => t.JobOrderId == jobOrderId && t.Type == TaskType.Delivery)
            .ToListAsync();
        
        foreach (var task in returnTasks)
        {
            var returnChecklist = await context.ChecklistItems
                .Where(c => c.TaskId == task.Id && c.Phase == ChecklistPhase.ReturnRental)
                .ToListAsync();
            
            foreach (var item in returnChecklist)
            {
                item.IsChecked = true;
                item.CheckedBy = skippedBy;
                item.CheckedAt = DateTime.UtcNow;
            }
        }
        
        await context.SaveChangesAsync();
        
        // Update job order status if all items resolved
        await UpdateJobOrderStatusAsync(jobOrderId, JobOrderStatus.Completed, skippedBy, $"Return phase skipped: {reason}");
        
        logger.LogWarning("Return phase skipped for JobOrder {JobNumber} by User {SkippedBy}. Reason: {Reason}", 
            jobOrder.JobNumber, skippedBy, reason);
        
        return true;
    }
    
    public async Task<bool> CompleteJobOrderAsync(int jobOrderId, int completedBy)
    {
        var jobOrder = await GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            return false;
        
        // Check all tasks are completed
        var incompleteTasks = await context.Tasks
            .AnyAsync(t => t.JobOrderId == jobOrderId && t.Status != Models.Enums.TaskStatus.Completed);
        
        if (incompleteTasks)
            throw new InvalidOperationException("Cannot complete job order with incomplete tasks");
        
        // Check rental items are returned or skipped
        var unreturnedRentals = await context.JobOrderItems
            .AnyAsync(i => i.JobOrderId == jobOrderId && i.IsRental && i.Status != JobOrderItemStatus.Returned && i.Status != JobOrderItemStatus.Lost);
        
        if (unreturnedRentals)
            throw new InvalidOperationException("Cannot complete job order with unreturned rental items");
        
        await UpdateJobOrderStatusAsync(jobOrderId, JobOrderStatus.Completed, completedBy);
        
        return true;
    }
    
    public async Task<bool> CancelJobOrderAsync(int jobOrderId, int cancelledBy, string reason)
    {
        var jobOrder = await GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            return false;
        
        if (jobOrder.Status == JobOrderStatus.Completed)
            throw new InvalidOperationException("Cannot cancel completed job order");
        
        // Return items to inventory if not used
        var items = await context.JobOrderItems
            .Where(i => i.JobOrderId == jobOrderId && i.Status != JobOrderItemStatus.Delivered)
            .ToListAsync();
        
        foreach (var item in items)
        {
            await inventoryService.ReturnToInventoryAsync(item.InventoryItemId, item.QuantityUsed, $"Order cancelled: {reason}");
            item.Status = JobOrderItemStatus.Cancelled;
        }
        
        await UpdateJobOrderStatusAsync(jobOrderId, JobOrderStatus.Cancelled, cancelledBy, reason);
        
        logger.LogInformation("JobOrder {JobNumber} cancelled by User {CancelledBy}. Reason: {Reason}", 
            jobOrder.JobNumber, cancelledBy, reason);
        
        return true;
    }
    
    // ========== Job Order Items Management ==========
    
    public async Task<JobOrderItem> AddJobOrderItemAsync(int jobOrderId, int inventoryItemId, int quantity, decimal? sellingPricePerUnit = null)
    {
        var jobOrder = await GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            throw new InvalidOperationException($"JobOrder with ID {jobOrderId} not found");
        
        if (jobOrder.Status == JobOrderStatus.Completed || jobOrder.Status == JobOrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot add items to {jobOrder.Status} job order");
        
        var inventoryItem = await context.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == inventoryItemId && !i.IsDeleted);
        
        if (inventoryItem == null)
            throw new InvalidOperationException($"Inventory item with ID {inventoryItemId} not found");
        
        if (inventoryItem.Quantity < quantity)
            throw new InvalidOperationException($"Insufficient stock. Available: {inventoryItem.Quantity}, Requested: {quantity}");
        
        // Reserve inventory
        await inventoryService.ReserveInventoryAsync(inventoryItemId, quantity, $"Added to JobOrder {jobOrder.JobNumber}");
        
        var jobOrderItem = new JobOrderItem
        {
            JobOrderId = jobOrderId,
            InventoryItemId = inventoryItemId,
            ItemName = inventoryItem.Title ?? "",
            Sku = inventoryItem.Sku,
            QuantityUsed = quantity,
            Unit = inventoryItem.Unit ?? "Piece",
            CostPerUnit = inventoryItem.PurchasePrice,
            SellingPricePerUnit = sellingPricePerUnit ?? inventoryItem.SalePrice,
            IsRental = false,
            Status = JobOrderItemStatus.Pending,
            Phase = ItemPhase.Preparation,
            AddedBy = jobOrder.UpdatedBy ?? 1,
            AddedAt = DateTime.UtcNow
        };
        
        context.JobOrderItems.Add(jobOrderItem);
        await context.SaveChangesAsync();
        
        await RecalculateJobOrderCostsAsync(jobOrderId);
        
        logger.LogInformation("Item added to JobOrder {JobNumber}: {ItemName} x{Quantity}", 
            jobOrder.JobNumber, inventoryItem.Title, quantity);
        
        return jobOrderItem;
    }
    
    public async Task<bool> RemoveJobOrderItemAsync(int jobOrderItemId)
    {
        var item = await context.JobOrderItems
            .Include(i => i.JobOrder)
            .FirstOrDefaultAsync(i => i.Id == jobOrderItemId);
        
        if (item == null)
            return false;

        var jobOrder = item.JobOrder ?? throw new InvalidOperationException($"JobOrder for item ID {jobOrderItemId} not found");
        
        if (jobOrder.Status == JobOrderStatus.Completed || jobOrder.Status == JobOrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot remove items from {jobOrder.Status} job order");
        
        // Return to inventory if not used
        if (item.Status != JobOrderItemStatus.Delivered && item.Status != JobOrderItemStatus.Prepared)
        {
            await inventoryService.ReturnToInventoryAsync(item.InventoryItemId, item.QuantityUsed, $"Removed from JobOrder {jobOrder.JobNumber}");
        }
        
        context.JobOrderItems.Remove(item);
        await context.SaveChangesAsync();
        
        await RecalculateJobOrderCostsAsync(item.JobOrderId);
        
        logger.LogInformation("Item removed from JobOrder {JobNumber}: {ItemName}", 
            jobOrder.JobNumber, item.ItemName);
        
        return true;
    }
    
    public async Task<JobOrderItem> UpdateJobOrderItemQuantityAsync(int jobOrderItemId, int quantity)
    {
        var item = await context.JobOrderItems
            .Include(i => i.JobOrder)
            .Include(i => i.InventoryItem)
            .FirstOrDefaultAsync(i => i.Id == jobOrderItemId);
        
        if (item == null)
            throw new InvalidOperationException($"JobOrderItem with ID {jobOrderItemId} not found");

        var jobOrder = item.JobOrder ?? throw new InvalidOperationException($"JobOrder for item ID {jobOrderItemId} not found");
        var inventoryItem = item.InventoryItem ?? throw new InvalidOperationException($"InventoryItem for item ID {jobOrderItemId} not found");
        
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero");
        
        var oldQuantity = item.QuantityUsed;
        var quantityDiff = quantity - oldQuantity;
        
        if (quantityDiff > 0)
        {
            // Need more stock
            if (inventoryItem.Quantity < quantityDiff)
                throw new InvalidOperationException($"Insufficient stock. Need {quantityDiff}, Available: {inventoryItem.Quantity}");
            
            await inventoryService.ReserveInventoryAsync(item.InventoryItemId, quantityDiff, $"Increased quantity for JobOrder {jobOrder.JobNumber}");
        }
        else if (quantityDiff < 0)
        {
            // Return excess stock
            await inventoryService.ReturnToInventoryAsync(item.InventoryItemId, -quantityDiff, $"Decreased quantity for JobOrder {jobOrder.JobNumber}");
        }
        
        item.QuantityUsed = quantity;
        item.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        await RecalculateJobOrderCostsAsync(item.JobOrderId);
        
        logger.LogInformation("Item quantity updated for JobOrder {JobNumber}: {ItemName} from {OldQty} to {NewQty}", 
            jobOrder.JobNumber, item.ItemName, oldQuantity, quantity);
        
        return item;
    }
    
    public async Task<JobOrderItem> MarkItemAsPreparedAsync(int jobOrderItemId, int preparedBy)
    {
        var item = await context.JobOrderItems
            .Include(i => i.JobOrder)
            .FirstOrDefaultAsync(i => i.Id == jobOrderItemId);
        
        if (item == null)
            throw new InvalidOperationException($"JobOrderItem with ID {jobOrderItemId} not found");

        _ = item.JobOrder ?? throw new InvalidOperationException($"JobOrder for item ID {jobOrderItemId} not found");
        
        if (item.Status != JobOrderItemStatus.Pending)
            throw new InvalidOperationException($"Item already {item.Status}");
        
        item.Status = JobOrderItemStatus.Prepared;
        item.PreparedAt = DateTime.UtcNow;
        item.PreparedBy = preparedBy;
        item.Phase = ItemPhase.Loading;
        item.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        logger.LogDebug("Item marked as prepared: {ItemName} for JobOrder {JobNumber} by User {PreparedBy}", 
            item.ItemName, item.JobOrder.JobNumber, preparedBy);
        
        return item;
    }
    
    public async Task<JobOrderItem> MarkItemAsDeliveredAsync(int jobOrderItemId, int deliveredBy)
    {
        var item = await context.JobOrderItems
            .Include(i => i.JobOrder)
            .FirstOrDefaultAsync(i => i.Id == jobOrderItemId);
        
        if (item == null)
            throw new InvalidOperationException($"JobOrderItem with ID {jobOrderItemId} not found");
        
        if (item.Status != JobOrderItemStatus.Prepared && item.Status != JobOrderItemStatus.Pending)
            throw new InvalidOperationException($"Item must be prepared before delivery. Current status: {item.Status}");
        
        item.Status = JobOrderItemStatus.Delivered;
        item.Phase = ItemPhase.Delivery;
        item.UpdatedAt = DateTime.UtcNow;
        
        // Update inventory to show consumed
        await inventoryService.ConsumeInventoryAsync(item.InventoryItemId, item.QuantityUsed, $"Delivered in JobOrder {item.JobOrder.JobNumber}");
        
        await context.SaveChangesAsync();
        
        logger.LogDebug("Item marked as delivered: {ItemName} for JobOrder {JobNumber} by User {DeliveredBy}", 
            item.ItemName, item.JobOrder.JobNumber, deliveredBy);
        
        return item;
    }
    
    public async Task<JobOrderItem> ReturnRentalItemAsync(int jobOrderItemId, int returnedToId, ReturnCondition condition, 
        decimal? damageDeduction = null, string? deductionReason = null, string? proofImageUrl = null)
    {
        var item = await context.JobOrderItems
            .Include(i => i.JobOrder)
            .FirstOrDefaultAsync(i => i.Id == jobOrderItemId);
        
        if (item == null)
            throw new InvalidOperationException($"JobOrderItem with ID {jobOrderItemId} not found");
        
        if (!item.IsRental)
            throw new InvalidOperationException("Item is not a rental item");
        
        if (item.Status != JobOrderItemStatus.Delivered)
            throw new InvalidOperationException($"Item must be delivered before return. Current status: {item.Status}");
        
        item.Status = condition == ReturnCondition.Good ? JobOrderItemStatus.Returned : JobOrderItemStatus.DamagedReturned;
        item.ReturnedAt = DateTime.UtcNow;
        item.ReturnedToId = returnedToId;
        item.ReturnCondition = condition;
        item.Phase = ItemPhase.Return;
        
        if (damageDeduction.HasValue && damageDeduction.Value > 0)
        {
            item.DamageDeduction = damageDeduction;
            item.DeductionReason = deductionReason;
        }
        
        if (!string.IsNullOrEmpty(proofImageUrl))
            item.ProofImageUrl = proofImageUrl;
        
        // Return to inventory if in good condition
        if (condition == ReturnCondition.Good)
        {
            await inventoryService.ReturnToInventoryAsync(item.InventoryItemId, item.QuantityUsed, $"Returned from JobOrder {item.JobOrder.JobNumber}");
        }
        
        await context.SaveChangesAsync();
        await RecalculateJobOrderCostsAsync(item.JobOrderId);
        
        logger.LogInformation("Rental item returned: {ItemName} for JobOrder {JobNumber} with condition {Condition}. Deduction: {Deduction}", 
            item.ItemName, item.JobOrder.JobNumber, condition, damageDeduction ?? 0);
        
        return item;
    }
    
    public async Task<IEnumerable<JobOrderItem>> GetJobOrderItemsAsync(int jobOrderId)
    {
        return await context.JobOrderItems
            .Include(i => i.InventoryItem)
            .Where(i => i.JobOrderId == jobOrderId)
            .OrderBy(i => i.Id)
            .ToListAsync();
    }
    
    // ========== Task Management ==========
    
    public async Task<IEnumerable<Models.Entities.Task>> GetJobOrderTasksAsync(int jobOrderId)
    {
        return await context.Tasks
            .Include(t => t.Assignee)
            .Include(t => t.SubTasks)
            .Include(t => t.ChecklistItems)
            .Where(t => t.JobOrderId == jobOrderId && !t.IsDeleted)
            .OrderBy(t => t.DueAt)
            .ToListAsync();
    }
    
    public async Task<JobOrder> AutoGenerateTasksAsync(int jobOrderId)
    {
        var jobOrder = await GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            throw new InvalidOperationException($"JobOrder with ID {jobOrderId} not found");
        
        // Clear existing tasks if any
        var existingTasks = await context.Tasks
            .Where(t => t.JobOrderId == jobOrderId)
            .ToListAsync();
        
        context.Tasks.RemoveRange(existingTasks);
        
        var tasks = new List<Models.Entities.Task>();
        
        // Task 1: Preparation (always)
        var preparationTask = new Models.Entities.Task
        {
            JobOrderId = jobOrderId,
            Type = TaskType.Preparation,
            Title = "Prepare Order Items",
            Description = jobOrder.SpecialInstructions,
            DueAt = jobOrder.DueAt.AddHours(-4),
            Status = Models.Enums.TaskStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        
        context.Tasks.Add(preparationTask);
        tasks.Add(preparationTask);
        
        // Add sub-tasks for each item
        var items = await GetJobOrderItemsAsync(jobOrderId);
        int order = 1;
        
        foreach (var item in items)
        {
            var subTask = new SubTask
            {
                Task = preparationTask,
                Title = $"Prepare {item.ItemName}",
                Description = $"Quantity: {item.QuantityUsed} {item.Unit}",
                Order = order++,
                IsCompleted = false
            };
            context.SubTasks.Add(subTask);
        }
        
        // Add checklist items for preparation
        var preparationChecklist = items.Select(item => new ChecklistItem
        {
            Task = preparationTask,
            Phase = ChecklistPhase.LoadingFromBranch,
            ItemName = $"{item.ItemName} (x{item.QuantityUsed})",
            IsChecked = false
        });
        
        context.ChecklistItems.AddRange(preparationChecklist);
        
        // Task 2: Design (if custom design required)
        if (jobOrder.SourceOrder?.Type == OrderType.Design || jobOrder.SourceOrder?.Type == OrderType.Custom)
        {
            var designTask = new Models.Entities.Task
            {
                JobOrderId = jobOrderId,
                Type = TaskType.Design,
                Title = "Complete Custom Design",
                Description = jobOrder.SourceOrder?.CustomDesignDescription,
                DueAt = jobOrder.DueAt.AddHours(-24),
                Status = Models.Enums.TaskStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            
            context.Tasks.Add(designTask);
            tasks.Add(designTask);
        }
        
        // Task 3: Delivery (if not pickup from branch)
        if (jobOrder.ExecutionType != ExecutionType.PickupFromBranch)
        {
            var deliveryTask = new Models.Entities.Task
            {
                JobOrderId = jobOrderId,
                Type = TaskType.Delivery,
                Title = "Deliver Order to Customer",
                Description = $"Address: {jobOrder.DeliveryAddress}",
                DueAt = jobOrder.DueAt,
                Status = Models.Enums.TaskStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            
            context.Tasks.Add(deliveryTask);
            
            // Add delivery checklist
            var deliveryChecklist = items.Select(item => new ChecklistItem
            {
                Task = deliveryTask,
                Phase = ChecklistPhase.DeliveryToCustomer,
                ItemName = $"{item.ItemName} (x{item.QuantityUsed})",
                IsChecked = false
            });
            
            context.ChecklistItems.AddRange(deliveryChecklist);
            
            // Add return checklist for rentals
            var rentalItems = items.Where(i => i.IsRental);
            if (rentalItems.Any())
            {
                var returnChecklist = rentalItems.Select(item => new ChecklistItem
                {
                    Task = deliveryTask,
                    Phase = ChecklistPhase.ReturnRental,
                    ItemName = $"{item.ItemName} (x{item.QuantityUsed}) - TO RETURN",
                    IsChecked = false
                });
                
                context.ChecklistItems.AddRange(returnChecklist);
            }
        }
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Auto-generated {TaskCount} tasks for JobOrder {JobNumber}", tasks.Count, jobOrder.JobNumber);
        
        return jobOrder;
    }
    
    // ========== Validation ==========
    
    public async Task<bool> JobOrderExistsAsync(int jobOrderId)
    {
        return await context.JobOrders
            .AnyAsync(j => j.Id == jobOrderId && !j.IsDeleted);
    }
    
    public async Task<bool> JobOrderNumberExistsAsync(string jobNumber, int? excludeJobOrderId = null)
    {
        var query = context.JobOrders.Where(j => j.JobNumber == jobNumber && !j.IsDeleted);
        
        if (excludeJobOrderId.HasValue)
            query = query.Where(j => j.Id != excludeJobOrderId.Value);
        
        return await query.AnyAsync();
    }
    
    // ========== Business Logic ==========
    
    public async Task<decimal> CalculateJobOrderTotalCostAsync(int jobOrderId)
    {
        var items = await context.JobOrderItems
            .Where(i => i.JobOrderId == jobOrderId)
            .ToListAsync();
        
        var itemCost = items.Sum(i => i.QuantityUsed * i.CostPerUnit);
        var damageDeductions = items.Sum(i => i.DamageDeduction ?? 0);
        
        return itemCost - damageDeductions;
    }
    
    public async Task<decimal> CalculateJobOrderProfitAsync(int jobOrderId)
    {
        var jobOrder = await GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            return 0;
        
        var totalCost = await CalculateJobOrderTotalCostAsync(jobOrderId);
        
        return jobOrder.TotalRevenue - totalCost;
    }
    
    public async Task<JobOrder> RecalculateJobOrderCostsAsync(int jobOrderId)
    {
        var jobOrder = await GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            throw new InvalidOperationException($"JobOrder with ID {jobOrderId} not found");
        
        var items = await context.JobOrderItems
            .Where(i => i.JobOrderId == jobOrderId && i.Status != JobOrderItemStatus.Cancelled)
            .ToListAsync();
        
        var totalCost = items.Sum(i => i.QuantityUsed * i.CostPerUnit);
        var damageDeductions = items.Sum(i => i.DamageDeduction ?? 0);
        var netCost = totalCost - damageDeductions;
        
        jobOrder.TotalCost = netCost;
        
        // Calculate revenue from source order or items
        if (jobOrder.SourceOrder != null)
        {
            jobOrder.TotalRevenue = jobOrder.SourceOrder.TotalAmount;
        }
        else
        {
            var totalRevenue = items.Sum(i => (i.SellingPricePerUnit ?? 0) * i.QuantityUsed);
            jobOrder.TotalRevenue = totalRevenue - damageDeductions;
        }
        
        jobOrder.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        
        logger.LogDebug("Recalculated costs for JobOrder {JobNumber}: Cost={Cost}, Revenue={Revenue}, Profit={Profit}", 
            jobOrder.JobNumber, jobOrder.TotalCost, jobOrder.TotalRevenue, jobOrder.TotalRevenue - jobOrder.TotalCost);
        
        return jobOrder;
    }
    
    // ========== Dashboard & Counters ==========
    
    public async Task<IEnumerable<JobOrder>> GetUpcomingJobOrdersAsync(int branchId, int daysAhead = 7)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(daysAhead);
        
        return await context.JobOrders
            .Include(j => j.Branch)
            .Include(j => j.Coordinator)
            .Where(j => j.BranchId == branchId 
                && !j.IsDeleted 
                && j.Status != JobOrderStatus.Completed 
                && j.Status != JobOrderStatus.Cancelled
                && j.DueAt <= cutoffDate)
            .OrderBy(j => j.DueAt)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<JobOrder>> GetOverdueJobOrdersAsync(int branchId)
    {
        return await context.JobOrders
            .Include(j => j.Branch)
            .Include(j => j.Coordinator)
            .Where(j => j.BranchId == branchId 
                && !j.IsDeleted 
                && j.Status != JobOrderStatus.Completed 
                && j.Status != JobOrderStatus.Cancelled
                && j.DueAt < DateTime.UtcNow)
            .OrderBy(j => j.DueAt)
            .ToListAsync();
    }
    
    public async Task<JobOrderDashboardDto> GetJobOrderDashboardAsync(int branchId)
    {
        var jobOrders = await context.JobOrders
            .Where(j => j.BranchId == branchId && !j.IsDeleted)
            .ToListAsync();
        
        var activeOrders = jobOrders.Where(j => j.Status != JobOrderStatus.Completed && j.Status != JobOrderStatus.Cancelled).ToList();
        
        var upcomingDeliveries = await GetUpcomingJobOrdersAsync(branchId, 3);
        
        return new JobOrderDashboardDto
        {
            TotalActiveOrders = activeOrders.Count,
            ReadyForDelivery = activeOrders.Count(j => j.Status == JobOrderStatus.ReadyForDelivery),
            OutForDelivery = activeOrders.Count(j => j.Status == JobOrderStatus.OutForDelivery),
            TodayDeliveries = activeOrders.Count(j => j.DueAt.Date == DateTime.UtcNow.Date),
            OverdueOrders = activeOrders.Count(j => j.DueAt < DateTime.UtcNow),
            WaitingReturn = activeOrders.Count(j => j.Status == JobOrderStatus.WaitingReturn),
            PendingTasks = await context.Tasks.CountAsync(t => t.JobOrder != null && t.JobOrder.BranchId == branchId && t.Status == Models.Enums.TaskStatus.Pending),
            InProgressTasks = await context.Tasks.CountAsync(t => t.JobOrder != null && t.JobOrder.BranchId == branchId && t.Status == Models.Enums.TaskStatus.InProgress),
            UpcomingDeliveries = upcomingDeliveries.Take(5).Select(j => new UpcomingDeliveryDto
            {
                JobOrderId = j.Id,
                JobNumber = j.JobNumber,
                DueAt = j.DueAt,
                CustomerName = j.SourceOrder?.Customer?.FullName ?? "N/A",
                DeliveryAddress = j.DeliveryAddress ?? "N/A",
                ItemsCount = j.JobOrderItems.Count
            }).ToList()
        };
    }
    
    // ========== Driver Specific ==========
    
    public async Task<IEnumerable<JobOrder>> GetDriverDeliveriesAsync(int driverId, DateTime? date = null)
    {
        var targetDate = date ?? DateTime.UtcNow.Date;
        var nextDay = targetDate.AddDays(1);
        
        var driverTasks = await context.Tasks
            .Where(t => t.Type == TaskType.Delivery 
                && t.AssignedTo == driverId 
                && t.DueAt >= targetDate 
                && t.DueAt < nextDay
                && !t.IsDeleted)
            .Select(t => t.JobOrderId)
            .Distinct()
            .ToListAsync();
        
        return await context.JobOrders
            .Include(j => j.Branch)
            .Include(j => j.SourceOrder)
                .ThenInclude(o => o != null ? o.Customer : null)
            .Include(j => j.JobOrderItems)
            .Where(j => driverTasks.Contains(j.Id) && !j.IsDeleted)
            .OrderBy(j => j.DueAt)
            .ToListAsync();
    }
    
    public async Task<DeliveryChecklistDto> GetDeliveryChecklistAsync(int jobOrderId)
    {
        var jobOrder = await GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            throw new InvalidOperationException($"JobOrder with ID {jobOrderId} not found");
        
        var items = await GetJobOrderItemsAsync(jobOrderId);
        
        var loadingItems = items.Where(i => i.Phase == ItemPhase.Loading || i.Status == JobOrderItemStatus.Prepared)
            .Select(i => new ChecklistPhaseDto
            {
                JobOrderItemId = i.Id,
                ItemName = i.ItemName,
                Quantity = i.QuantityUsed,
                IsChecked = i.Status == JobOrderItemStatus.Prepared,
                ProofImageUrl = i.ProofImageUrl
            }).ToList();
        
        var deliveryItems = items.Where(i => i.Phase == ItemPhase.Delivery || i.Status == JobOrderItemStatus.Delivered)
            .Select(i => new ChecklistPhaseDto
            {
                JobOrderItemId = i.Id,
                ItemName = i.ItemName,
                Quantity = i.QuantityUsed,
                IsChecked = i.Status == JobOrderItemStatus.Delivered,
                ProofImageUrl = i.ProofImageUrl
            }).ToList();
        
        var returnItems = items.Where(i => i.IsRental && (i.Phase == ItemPhase.Return || i.Status == JobOrderItemStatus.Returned || i.Status == JobOrderItemStatus.DamagedReturned))
            .Select(i => new ChecklistPhaseDto
            {
                JobOrderItemId = i.Id,
                ItemName = i.ItemName,
                Quantity = i.QuantityUsed,
                IsChecked = i.Status == JobOrderItemStatus.Returned || i.Status == JobOrderItemStatus.DamagedReturned,
                ProofImageUrl = i.ProofImageUrl
            }).ToList();
        
        return new DeliveryChecklistDto
        {
            JobOrderId = jobOrder.Id,
            JobNumber = jobOrder.JobNumber,
            DeliveryAddress = jobOrder.DeliveryAddress ?? "N/A",
            CustomerName = jobOrder.SourceOrder?.Customer?.FullName ?? "N/A",
            CustomerPhone = jobOrder.SourceOrder?.Customer?.Phone ?? "N/A",
            LoadingItems = loadingItems,
            DeliveryItems = deliveryItems,
            ReturnItems = returnItems
        };
    }
    
    public async Task<bool> UpdateDeliveryChecklistAsync(int jobOrderId, int driverId, List<ChecklistUpdateDto> checklistUpdates)
    {
        var jobOrder = await GetJobOrderByIdAsync(jobOrderId);
        if (jobOrder == null)
            return false;
        
        foreach (var update in checklistUpdates)
        {
            var item = await context.JobOrderItems
                .FirstOrDefaultAsync(i => i.Id == update.JobOrderItemId && i.JobOrderId == jobOrderId);
            
            if (item == null)
                continue;
            
            if (update.IsChecked)
            {
                // Mark appropriate phase based on item status
                if (item.Status == JobOrderItemStatus.Pending)
                {
                    await MarkItemAsPreparedAsync(item.Id, driverId);
                }
                else if (item.Status == JobOrderItemStatus.Prepared && item.Phase == ItemPhase.Loading)
                {
                    await MarkItemAsDeliveredAsync(item.Id, driverId);
                }
                else if (item.IsRental && item.Status == JobOrderItemStatus.Delivered)
                {
                    await ReturnRentalItemAsync(item.Id, driverId, ReturnCondition.Good, null, null, update.ProofImageUrl);
                }
            }
            
            if (!string.IsNullOrEmpty(update.ProofImageUrl))
            {
                item.ProofImageUrl = update.ProofImageUrl;
            }
        }
        
        await context.SaveChangesAsync();
        
        // Check if all delivery items are delivered
        var allItems = await GetJobOrderItemsAsync(jobOrderId);
        var allDelivered = allItems.All(i => i.Status == JobOrderItemStatus.Delivered || i.Status == JobOrderItemStatus.Returned);
        
        if (allDelivered)
        {
            await UpdateJobOrderStatusAsync(jobOrderId, JobOrderStatus.Delivered, driverId);
        }
        
        logger.LogInformation("Delivery checklist updated for JobOrder {JobNumber} by Driver {DriverId}", 
            jobOrder.JobNumber, driverId);
        
        return true;
    }
    
    // ========== Designer Specific ==========
    
    public async Task<IEnumerable<JobOrder>> GetDesignerJobOrdersAsync(int designerId, Models.Enums.TaskStatus? status = null)
    {
        var designerTasks = context.Tasks
            .Where(t => t.Type == TaskType.Design && t.AssignedTo == designerId && !t.IsDeleted);
        
        if (status.HasValue)
            designerTasks = designerTasks.Where(t => t.Status == status.Value);
        
        var jobOrderIds = await designerTasks.Select(t => t.JobOrderId).Distinct().ToListAsync();
        
        return await context.JobOrders
            .Include(j => j.Branch)
            .Include(j => j.SourceOrder)
            .Include(j => j.Tasks)
            .Where(j => jobOrderIds.Contains(j.Id) && !j.IsDeleted)
            .OrderBy(j => j.DueAt)
            .ToListAsync();
    }
    
    // ========== Reports ==========
    
    public async Task<JobOrderReportDto> GetJobOrderReportAsync(DateTime startDate, DateTime endDate, int? branchId = null)
    {
        var query = context.JobOrders
            .Include(j => j.Branch)
            .Where(j => j.CreatedAt >= startDate && j.CreatedAt <= endDate && !j.IsDeleted);
        
        if (branchId.HasValue)
            query = query.Where(j => j.BranchId == branchId.Value);
        
        var jobOrders = await query.ToListAsync();
        
        var completedOrders = jobOrders.Where(j => j.Status == JobOrderStatus.Completed).ToList();
        var cancelledOrders = jobOrders.Where(j => j.Status == JobOrderStatus.Cancelled).ToList();
        var overdueOrders = jobOrders.Where(j => j.Status != JobOrderStatus.Completed && j.Status != JobOrderStatus.Cancelled && j.DueAt < DateTime.UtcNow).ToList();
        
        var totalRevenue = completedOrders.Sum(j => j.TotalRevenue);
        var totalCost = completedOrders.Sum(j => j.TotalCost);
        var totalProfit = totalRevenue - totalCost;
        
        return new JobOrderReportDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalJobOrders = jobOrders.Count,
            CompletedOrders = completedOrders.Count,
            CancelledOrders = cancelledOrders.Count,
            OverdueOrders = overdueOrders.Count,
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            TotalProfit = totalProfit,
            AverageProfitPerOrder = completedOrders.Any() ? totalProfit / completedOrders.Count : 0,
            JobOrders = jobOrders.Select(j => new JobOrderSummaryDto
            {
                Id = j.Id,
                JobNumber = j.JobNumber,
                CreatedAt = j.CreatedAt,
                DueAt = j.DueAt,
                CompletedAt = j.Status == JobOrderStatus.Completed ? j.UpdatedAt : null,
                Status = j.Status,
                BranchName = j.Branch?.Name ?? "N/A",
                CustomerName = j.SourceOrder?.Customer?.FullName ?? "N/A",
                Revenue = j.TotalRevenue,
                Cost = j.TotalCost,
                Profit = j.TotalRevenue - j.TotalCost
            }).ToList()
        };
    }
    
    // ========== Private Helper Methods ==========
    
    private async Task<string> GenerateJobNumberAsync()
    {
        var date = DateTime.UtcNow;
        var prefix = $"JO-{date:yyyy}-";
        
        var lastJob = await context.JobOrders
            .Where(j => j.JobNumber != null && j.JobNumber.StartsWith(prefix))
            .OrderByDescending(j => j.JobNumber)
            .FirstOrDefaultAsync();
        
        if (lastJob?.JobNumber is not { Length: > 0 })
            return $"{prefix}0001";

        var parts = lastJob.JobNumber.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3 || !int.TryParse(parts[^1], out var lastNumber))
            return $"{prefix}0001";

        var newNumber = lastNumber + 1;
        return $"{prefix}{newNumber:D4}";
    }
    
    private bool IsValidStatusTransition(JobOrderStatus from, JobOrderStatus to)
    {
        return (from, to) switch
        {
            (_, JobOrderStatus.Completed) when from == JobOrderStatus.Delivered || from == JobOrderStatus.ReadyForDelivery => true,
            (JobOrderStatus.Pending, JobOrderStatus.InProgress) => true,
            (JobOrderStatus.InProgress, JobOrderStatus.ReadyForDelivery) => true,
            (JobOrderStatus.ReadyForDelivery, JobOrderStatus.OutForDelivery) => true,
            (JobOrderStatus.OutForDelivery, JobOrderStatus.Delivered) => true,
            (JobOrderStatus.Delivered, JobOrderStatus.WaitingReturn) => true,
            (JobOrderStatus.WaitingReturn, JobOrderStatus.Completed) => true,
            (_, JobOrderStatus.Cancelled) when from != JobOrderStatus.Completed => true,
            _ => false
        };
    }
}
