using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;

namespace Palloncino.Services.Implementations;

public class CatalogService(
    ApplicationDbContext context,
    ILogger<CatalogService> logger) : ICatalogService
{
    // ========== Customer Endpoints ==========
    
    public async Task<IEnumerable<CatalogItem>> GetAllCatalogItemsAsync(string? category = null, bool includeOutOfStock = false)
    {
        var query = context.CatalogItems
            .Where(c => c.IsActive && !c.IsDeleted);
        
        if (!includeOutOfStock)
            query = query.Where(c => c.Status != ItemStatus.OutOfStock);
        
        if (!string.IsNullOrEmpty(category))
            query = query.Where(c => c.Category == category);
        
        return await query
            .OrderBy(c => c.Title)
            .ToListAsync();
    }
    
    public async Task<CatalogItem?> GetCatalogItemByIdAsync(int id)
    {
        return await context.CatalogItems
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive && !c.IsDeleted);
    }
    
    public async Task<IEnumerable<Template>> GetAllTemplatesAsync(string? category = null)
    {
        var query = context.Templates
            .Include(t => t.TemplateItems)
                .ThenInclude(ti => ti.CatalogItem)
            .Where(t => t.IsActive && !t.IsDeleted);
        
        if (!string.IsNullOrEmpty(category))
            query = query.Where(t => t.Category == category);
        
        return await query
            .OrderBy(t => t.Title)
            .ToListAsync();
    }
    
    public async Task<Template?> GetTemplateByIdAsync(int id)
    {
        return await context.Templates
            .Include(t => t.TemplateItems)
                .ThenInclude(ti => ti.CatalogItem)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsActive && !t.IsDeleted);
    }
    
    // ========== Admin Endpoints ==========
    
    public async Task<CatalogItem> CreateCatalogItemAsync(CatalogItem item)
    {
        // Validate SKU uniqueness
        if (!string.IsNullOrEmpty(item.Sku) && await SkuExistsAsync(item.Sku))
            throw new InvalidOperationException($"SKU '{item.Sku}' already exists");
        
        // Validate price
        if (item.Price <= 0)
            throw new InvalidOperationException("Price must be greater than zero");
        
        // Validate rental flag
        if (item.IsRental && item.StockQuantity.HasValue && item.StockQuantity.Value < 0)
            throw new InvalidOperationException("Stock quantity cannot be negative for rental items");
        
        if (!item.IsRental && item.StockQuantity.HasValue && item.StockQuantity.Value < 0)
            throw new InvalidOperationException("Stock quantity cannot be negative");
        
        // Set default status based on stock
        if (!item.IsRental && item.StockQuantity.HasValue && item.StockQuantity.Value <= 0)
            item.Status = ItemStatus.OutOfStock;
        else
            item.Status = ItemStatus.Available;
        
        item.CreatedAt = DateTime.UtcNow;
        item.IsActive = true;
        
        context.CatalogItems.Add(item);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Catalog item created: {Title} (SKU: {Sku})", item.Title, item.Sku);
        
        return item;
    }
    
    public async Task<CatalogItem> UpdateCatalogItemAsync(CatalogItem item)
    {
        var existingItem = await GetCatalogItemByIdAsync(item.Id);
        if (existingItem == null)
            throw new InvalidOperationException($"Catalog item with ID {item.Id} not found");
        
        // Check SKU uniqueness if changed
        if (!string.IsNullOrEmpty(item.Sku) && item.Sku != existingItem.Sku && await SkuExistsAsync(item.Sku, item.Id))
            throw new InvalidOperationException($"SKU '{item.Sku}' already exists");
        
        // Update fields
        existingItem.Title = item.Title;
        existingItem.Description = item.Description;
        existingItem.Category = item.Category;
        existingItem.Price = item.Price;
        existingItem.IsRental = item.IsRental;
        existingItem.ImageUrl = item.ImageUrl;
        existingItem.Sku = item.Sku;
        existingItem.StockQuantity = item.StockQuantity;
        
        // Update status based on stock
        if (!existingItem.IsRental && existingItem.StockQuantity.HasValue)
        {
            existingItem.Status = existingItem.StockQuantity.Value <= 0 
                ? ItemStatus.OutOfStock 
                : ItemStatus.Available;
        }
        
        existingItem.UpdatedAt = DateTime.UtcNow;
        existingItem.UpdatedBy = item.UpdatedBy;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Catalog item updated: {Title} (ID: {Id})", existingItem.Title, item.Id);
        
        return existingItem;
    }
    
    public async Task<bool> DeleteCatalogItemAsync(int id)
    {
        var item = await GetCatalogItemByIdAsync(id);
        if (item == null)
            return false;
        
        // Check if item is used in any templates
        var isUsedInTemplates = await context.TemplateItems
            .AnyAsync(ti => ti.CatalogItemId == id);
        
        if (isUsedInTemplates)
            throw new InvalidOperationException("Cannot delete catalog item that is used in templates");
        
        // Check if item is used in any active orders
        var isUsedInOrders = await context.OrderItems
            .AnyAsync(oi => oi.CatalogItemId == id);
        
        if (isUsedInOrders)
            throw new InvalidOperationException("Cannot delete catalog item that has been ordered. Soft delete instead.");
        
        context.CatalogItems.Remove(item);
        await context.SaveChangesAsync();
        
        logger.LogWarning("Catalog item permanently deleted: {Title} (ID: {Id})", item.Title, id);
        
        return true;
    }
    
    public async Task<bool> SoftDeleteCatalogItemAsync(int id, int deletedBy)
    {
        var item = await GetCatalogItemByIdAsync(id);
        if (item == null)
            return false;
        
        item.SoftDelete(deletedBy);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Catalog item soft deleted: {Title} (ID: {Id}) by User {DeletedBy}", 
            item.Title, id, deletedBy);
        
        return true;
    }
    
    // ========== Helper Methods ==========
    
    public async Task<bool> CatalogItemExistsAsync(int id)
    {
        return await context.CatalogItems
            .AnyAsync(c => c.Id == id && c.IsActive && !c.IsDeleted);
    }
    
    public async Task<bool> SkuExistsAsync(string sku, int? excludeId = null)
    {
        var query = context.CatalogItems
            .Where(c => c.Sku == sku && !c.IsDeleted);
        
        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);
        
        return await query.AnyAsync();
    }
    
    public async Task<decimal> GetTemplateDiscountedPriceAsync(int templateId)
    {
        var template = await GetTemplateByIdAsync(templateId);
        if (template == null)
            return 0;
        
        return template.AfterDiscount;
    }
}