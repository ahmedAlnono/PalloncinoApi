using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Services.Interfaces;
using Task = System.Threading.Tasks.Task;

namespace Palloncino.Services.Implementations;

public class InventoryService(
    ApplicationDbContext context,
    ILogger<InventoryService> logger,
    IMapper mapper) : IInventoryService
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
    public async Task<IEnumerable<InventoryItemDto>> GetInventoryItemsAsync(int page, int pageSize)
    {
        try
        {
            var items = await context.InventoryItems
            .AsNoTracking()
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
            await context.InventoryItems.AddAsync(item);
            await context.SaveChangesAsync();
            return item;
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
            var item = await context.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId)
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
            await context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            logger.LogError("fail to update item with it {}",itemId);
            throw new Exception("fail to update item");
        }
    }
}