using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Services.Interfaces;

namespace Palloncino.Services.Implementations;

public class InventoryService(
    ApplicationDbContext context,
    ILogger<InventoryService> logger) : IInventoryService
{
    public async Task ReserveInventoryAsync(int inventoryItemId, int quantity, string reason)
    {
        var item = await context.InventoryItems.FindAsync(inventoryItemId);
        if (item == null) throw new InvalidOperationException("Inventory item not found");

        if (item.Quantity < quantity)
            throw new InvalidOperationException($"Insufficient stock for item {item.Title}");

        item.Quantity -= quantity;
        item.UpdatedAt = DateTime.UtcNow;
        
        logger.LogInformation("Inventory Reserved: {Quantity} of {ItemTitle}. Reason: {Reason}", quantity, item.Title, reason);
        await context.SaveChangesAsync();
    }

    public async Task ConsumeInventoryAsync(int inventoryItemId, int quantity, string reason)
    {
        var item = await context.InventoryItems.FindAsync(inventoryItemId);
        if (item == null) throw new InvalidOperationException("Inventory item not found");

        // Quantity already reduced during reservation, so we just log it here
        // In a more complex system, we might move from 'Reserved' to 'Consumed' status
        logger.LogInformation("Inventory Consumed: {Quantity} of {ItemTitle}. Reason: {Reason}", quantity, item.Title, reason);
        await Task.CompletedTask;
    }

    public async Task ReturnToInventoryAsync(int inventoryItemId, int quantity, string reason)
    {
        var item = await context.InventoryItems.FindAsync(inventoryItemId);
        if (item == null) throw new InvalidOperationException("Inventory item not found");

        item.Quantity += quantity;
        item.UpdatedAt = DateTime.UtcNow;

        logger.LogInformation("Inventory Returned: {Quantity} of {ItemTitle}. Reason: {Reason}", quantity, item.Title, reason);
        await context.SaveChangesAsync();
    }
}