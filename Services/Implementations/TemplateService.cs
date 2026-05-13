using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.Entities;
using Palloncino.Services.Interfaces;
using Palloncino.Models.DTOs;

namespace Palloncino.Services.Implementations;

public class TemplateService(
    ApplicationDbContext context,
    ILogger<TemplateService> logger) : ITemplateService
{
    // ========== CRUD Operations ==========
    
    public async Task<Template> CreateTemplateAsync(Template template, List<TemplateItem> items)
    {
        // Validate unique name
        if (await TemplateNameExistsAsync(template.Title))
            throw new InvalidOperationException($"Template name '{template.Title}' already exists");
        
        // Validate discount logic
        if (template.AfterDiscount > template.BeforeDiscount)
            throw new InvalidOperationException("After discount cannot be greater than before discount");
        
        if (template.AfterDiscount <= 0)
            throw new InvalidOperationException("After discount must be greater than zero");
        
        // Validate all catalog items exist
        foreach (var item in items)
        {
            var catalogItem = await context.CatalogItems
                .FirstOrDefaultAsync(c => c.Id == item.CatalogItemId && c.IsActive);
            
            if (catalogItem == null)
                throw new InvalidOperationException($"Catalog item with ID {item.CatalogItemId} not found");
            
            if (item.Quantity <= 0)
                throw new InvalidOperationException($"Quantity must be greater than zero for item {catalogItem.Title}");
        }
        
        template.IsActive = true;
        template.CreatedAt = DateTime.UtcNow;
        
        context.Templates.Add(template);
        await context.SaveChangesAsync();
        
        // Add items
        foreach (var item in items)
        {
            item.TemplateId = template.Id;
            context.TemplateItems.Add(item);
        }
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Template created: {TemplateName} (ID: {TemplateId}) with {ItemCount} items", 
            template.Title, template.Id, items.Count);
        
        return template;
    }
    
    public async Task<Template> UpdateTemplateAsync(Template template, List<TemplateItem>? items = null)
    {
        var existingTemplate = await GetTemplateByIdAsync(template.Id);
        if (existingTemplate == null)
            throw new InvalidOperationException($"Template with ID {template.Id} not found");
        
        // Check name uniqueness
        if (template.Title != existingTemplate.Title && await TemplateNameExistsAsync(template.Title, template.Id))
            throw new InvalidOperationException($"Template name '{template.Title}' already exists");
        
        // Validate discount logic
        if (template.AfterDiscount > template.BeforeDiscount)
            throw new InvalidOperationException("After discount cannot be greater than before discount");
        
        if (template.AfterDiscount <= 0)
            throw new InvalidOperationException("After discount must be greater than zero");
        
        // Update template fields
        existingTemplate.Title = template.Title;
        existingTemplate.Description = template.Description;
        existingTemplate.BeforeDiscount = template.BeforeDiscount;
        existingTemplate.AfterDiscount = template.AfterDiscount;
        existingTemplate.ImageUrl = template.ImageUrl;
        existingTemplate.Category = template.Category;
        existingTemplate.IsActive = template.IsActive;
        existingTemplate.UpdatedAt = DateTime.UtcNow;
        existingTemplate.UpdatedBy = template.UpdatedBy;
        
        // Update items if provided
        if (items != null)
        {
            // Remove existing items
            var existingItems = context.TemplateItems.Where(ti => ti.TemplateId == template.Id);
            context.TemplateItems.RemoveRange(existingItems);
            
            // Add new items
            foreach (var item in items)
            {
                var catalogItem = await context.CatalogItems
                    .FirstOrDefaultAsync(c => c.Id == item.CatalogItemId && c.IsActive);
                
                if (catalogItem == null)
                    throw new InvalidOperationException($"Catalog item with ID {item.CatalogItemId} not found");
                
                item.TemplateId = template.Id;
                context.TemplateItems.Add(item);
            }
        }
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Template updated: {TemplateName} (ID: {TemplateId})", existingTemplate.Title, template.Id);
        
        return existingTemplate;
    }
    
    public async Task<bool> DeleteTemplateAsync(int templateId)
    {
        var template = await GetTemplateByIdAsync(templateId);
        if (template == null)
            return false;
        
        // Check if template is used in any orders
        var isUsed = await context.Orders
            .AnyAsync(o => o.OrderItems.Any(oi => oi.CatalogItemId != null 
                && context.TemplateItems.Any(ti => ti.CatalogItemId == oi.CatalogItemId && ti.TemplateId == templateId)));
        
        if (isUsed)
            throw new InvalidOperationException($"Cannot delete template that is used in existing orders. Soft delete instead.");
        
        // Remove template items first
        var templateItems = context.TemplateItems.Where(ti => ti.TemplateId == templateId);
        context.TemplateItems.RemoveRange(templateItems);
        
        // Remove template
        context.Templates.Remove(template);
        await context.SaveChangesAsync();
        
        logger.LogWarning("Template permanently deleted: {TemplateName} (ID: {TemplateId})", template.Title, templateId);
        
        return true;
    }
    
    public async Task<bool> SoftDeleteTemplateAsync(int templateId, int deletedBy)
    {
        var template = await GetTemplateByIdAsync(templateId);
        if (template == null)
            return false;
        
        template.SoftDelete(deletedBy);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Template soft deleted: {TemplateName} (ID: {TemplateId}) by User {DeletedBy}", 
            template.Title, templateId, deletedBy);
        
        return true;
    }
    
    // ========== Queries ==========
    
    public async Task<Template?> GetTemplateByIdAsync(int templateId)
    {
        return await context.Templates
            .Include(t => t.TemplateItems)
                .ThenInclude(ti => ti.CatalogItem)
            .FirstOrDefaultAsync(t => t.Id == templateId && !t.IsDeleted);
    }
    
    public async Task<Template?> GetTemplateByNameAsync(string name)
    {
        return await context.Templates
            .Include(t => t.TemplateItems)
                .ThenInclude(ti => ti.CatalogItem)
            .FirstOrDefaultAsync(t => t.Title == name && !t.IsDeleted);
    }
    
    public async Task<IEnumerable<Template>> GetAllTemplatesAsync(bool includeInactive = false)
    {
        var query = context.Templates
            .Include(t => t.TemplateItems)
                .ThenInclude(ti => ti.CatalogItem)
            .Where(t => !t.IsDeleted);
        
        if (!includeInactive)
            query = query.Where(t => t.IsActive);
        
        return await query
            .OrderBy(t => t.Title)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Template>> GetActiveTemplatesAsync()
    {
        return await context.Templates
            .Include(t => t.TemplateItems)
                .ThenInclude(ti => ti.CatalogItem)
            .Where(t => t.IsActive && !t.IsDeleted)
            .OrderBy(t => t.Title)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Template>> GetTemplatesByCategoryAsync(string category)
    {
        return await context.Templates
            .Include(t => t.TemplateItems)
                .ThenInclude(ti => ti.CatalogItem)
            .Where(t => t.Category == category && t.IsActive && !t.IsDeleted)
            .OrderBy(t => t.Title)
            .ToListAsync();
    }
    
    // ========== Template Items Management ==========
    
    public async Task<TemplateItem> AddTemplateItemAsync(int templateId, int catalogItemId, int quantity)
    {
        var template = await GetTemplateByIdAsync(templateId);
        if (template == null)
            throw new InvalidOperationException($"Template with ID {templateId} not found");
        
        var catalogItem = await context.CatalogItems
            .FirstOrDefaultAsync(c => c.Id == catalogItemId && c.IsActive);
        
        if (catalogItem == null)
            throw new InvalidOperationException($"Catalog item with ID {catalogItemId} not found");
        
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero");
        
        // Check if item already exists in template
        var existingItem = await context.TemplateItems
            .FirstOrDefaultAsync(ti => ti.TemplateId == templateId && ti.CatalogItemId == catalogItemId);
        
        if (existingItem != null)
        {
            // Update quantity instead
            existingItem.Quantity = quantity;
            await context.SaveChangesAsync();
            
            logger.LogInformation("Updated template item quantity: Template {TemplateId}, CatalogItem {CatalogItemId}, Quantity {Quantity}", 
                templateId, catalogItemId, quantity);
            
            return existingItem;
        }
        
        var templateItem = new TemplateItem
        {
            TemplateId = templateId,
            CatalogItemId = catalogItemId,
            Quantity = quantity,
            CreatedAt = DateTime.UtcNow
        };
        
        context.TemplateItems.Add(templateItem);
        await context.SaveChangesAsync();
        
        // Recalculate template pricing
        await RecalculateTemplatePricingAsync(templateId);
        
        logger.LogInformation("Item added to template: Template {TemplateId}, CatalogItem {CatalogItemId}, Quantity {Quantity}", 
            templateId, catalogItemId, quantity);
        
        return templateItem;
    }
    
    public async Task<bool> RemoveTemplateItemAsync(int templateItemId)
    {
        var templateItem = await context.TemplateItems
            .Include(ti => ti.Template)
            .FirstOrDefaultAsync(ti => ti.Id == templateItemId);
        
        if (templateItem == null)
            return false;
        
        var templateId = templateItem.TemplateId;
        
        context.TemplateItems.Remove(templateItem);
        await context.SaveChangesAsync();
        
        // Recalculate template pricing
        await RecalculateTemplatePricingAsync(templateId);
        
        logger.LogInformation("Item removed from template: TemplateItemId {TemplateItemId}", templateItemId);
        
        return true;
    }
    
    public async Task<TemplateItem> UpdateTemplateItemQuantityAsync(int templateItemId, int quantity)
    {
        var templateItem = await context.TemplateItems
            .Include(ti => ti.Template)
            .FirstOrDefaultAsync(ti => ti.Id == templateItemId);
        
        if (templateItem == null)
            throw new InvalidOperationException($"Template item with ID {templateItemId} not found");
        
        if (quantity <= 0)
            throw new InvalidOperationException("Quantity must be greater than zero");
        
        templateItem.Quantity = quantity;
        templateItem.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        // Recalculate template pricing
        await RecalculateTemplatePricingAsync(templateItem.TemplateId);
        
        logger.LogInformation("Template item quantity updated: Item {TemplateItemId}, New Quantity {Quantity}", 
            templateItemId, quantity);
        
        return templateItem;
    }
    
    public async Task<IEnumerable<TemplateItem>> GetTemplateItemsAsync(int templateId)
    {
        return await context.TemplateItems
            .Include(ti => ti.CatalogItem)
            .Where(ti => ti.TemplateId == templateId)
            .ToListAsync();
    }
    
    // ========== Validation ==========
    
    public async Task<bool> TemplateExistsAsync(int templateId)
    {
        return await context.Templates
            .AnyAsync(t => t.Id == templateId && !t.IsDeleted);
    }
    
    public async Task<bool> TemplateNameExistsAsync(string name, int? excludeTemplateId = null)
    {
        var query = context.Templates.Where(t => t.Title == name && !t.IsDeleted);
        
        if (excludeTemplateId.HasValue)
            query = query.Where(t => t.Id != excludeTemplateId.Value);
        
        return await query.AnyAsync();
    }
    
    // ========== Business Logic ==========
    
    public async Task<decimal> CalculateTemplateTotalPriceAsync(int templateId)
    {
        var items = await context.TemplateItems
            .Include(ti => ti.CatalogItem)
            .Where(ti => ti.TemplateId == templateId)
            .ToListAsync();
        
        return items.Sum(item => (item.CatalogItem?.Price ?? 0) * item.Quantity);
    }
    
    public async Task<decimal> CalculateTemplateDiscountPercentageAsync(int templateId)
    {
        var template = await GetTemplateByIdAsync(templateId);
        if (template == null)
            return 0;
        
        if (template.BeforeDiscount <= 0)
            return 0;
        
        return ((template.BeforeDiscount - template.AfterDiscount) / template.BeforeDiscount) * 100;
    }
    
    public async Task<Template> DuplicateTemplateAsync(int templateId, string newName, int duplicatedBy)
    {
        var originalTemplate = await GetTemplateByIdAsync(templateId);
        if (originalTemplate == null)
            throw new InvalidOperationException($"Template with ID {templateId} not found");
        
        // Check if new name already exists
        if (await TemplateNameExistsAsync(newName))
            throw new InvalidOperationException($"Template name '{newName}' already exists");
        
        // Create copy
        var newTemplate = new Template
        {
            Title = newName,
            Description = originalTemplate.Description,
            BeforeDiscount = originalTemplate.BeforeDiscount,
            AfterDiscount = originalTemplate.AfterDiscount,
            ImageUrl = originalTemplate.ImageUrl,
            Category = originalTemplate.Category,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = duplicatedBy
        };
        
        context.Templates.Add(newTemplate);
        await context.SaveChangesAsync();
        
        // Copy items
        foreach (var item in originalTemplate.TemplateItems)
        {
            var newItem = new TemplateItem
            {
                TemplateId = newTemplate.Id,
                CatalogItemId = item.CatalogItemId,
                Quantity = item.Quantity,
                CreatedAt = DateTime.UtcNow
            };
            
            context.TemplateItems.Add(newItem);
        }
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Template duplicated: Original {OriginalId} ({OriginalName}) -> New {NewId} ({NewName}) by User {DuplicatedBy}", 
            templateId, originalTemplate.Title, newTemplate.Id, newName, duplicatedBy);
        
        return newTemplate;
    }
    
    // ========== Status Management ==========
    
    public async Task<bool> ActivateTemplateAsync(int templateId)
    {
        var template = await GetTemplateByIdAsync(templateId);
        if (template == null)
            return false;
        
        if (template.TemplateItems == null || !template.TemplateItems.Any())
            throw new InvalidOperationException("Cannot activate template with no items");
        
        template.IsActive = true;
        template.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Template activated: {TemplateName} (ID: {TemplateId})", template.Title, templateId);
        
        return true;
    }
    
    public async Task<bool> DeactivateTemplateAsync(int templateId)
    {
        var template = await GetTemplateByIdAsync(templateId);
        if (template == null)
            return false;
        
        template.IsActive = false;
        template.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Template deactivated: {TemplateName} (ID: {TemplateId})", template.Title, templateId);
        
        return true;
    }
    
    // ========== Bulk Operations ==========
    
    public async Task<int> BulkUpdateTemplatePricesAsync(decimal percentageIncrease)
    {
        if (percentageIncrease <= -100)
            throw new InvalidOperationException("Percentage increase cannot be less than -100%");
        
        var templates = await context.Templates
            .Where(t => !t.IsDeleted)
            .ToListAsync();
        
        foreach (var template in templates)
        {
            template.BeforeDiscount = template.BeforeDiscount * (1 + percentageIncrease / 100);
            template.AfterDiscount = template.AfterDiscount * (1 + percentageIncrease / 100);
            template.UpdatedAt = DateTime.UtcNow;
        }
        
        var updatedCount = templates.Count;
        await context.SaveChangesAsync();
        
        logger.LogInformation("Bulk price update: {PercentageIncrease}% applied to {Count} templates", 
            percentageIncrease, updatedCount);
        
        return updatedCount;
    }
    
    public async Task<int> BulkDeactivateExpiredTemplatesAsync(int daysSinceLastUpdate)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysSinceLastUpdate);
        
        var templates = await context.Templates
            .Where(t => t.IsActive 
                && !t.IsDeleted 
                && t.UpdatedAt.HasValue 
                && t.UpdatedAt.Value < cutoffDate)
            .ToListAsync();
        
        foreach (var template in templates)
        {
            template.IsActive = false;
            template.UpdatedAt = DateTime.UtcNow;
        }
        
        var deactivatedCount = templates.Count;
        await context.SaveChangesAsync();
        
        logger.LogInformation("Bulk deactivation: {Count} templates deactivated (inactive for {Days} days)", 
            deactivatedCount, daysSinceLastUpdate);
        
        return deactivatedCount;
    }
    
    // ========== Statistics ==========
    
    public async Task<TemplateStatisticsDto> GetTemplateStatisticsAsync(int templateId)
    {
        var template = await GetTemplateByIdAsync(templateId);
        if (template == null)
            throw new InvalidOperationException($"Template with ID {templateId} not found");
        
        var totalItems = template.TemplateItems?.Count ?? 0;
        var totalQuantity = template.TemplateItems?.Sum(ti => ti.Quantity) ?? 0;
        var originalPrice = await CalculateTemplateTotalPriceAsync(templateId);
        
        // Count how many times this template was ordered
        var orderCount = await context.OrderItems
            .Where(oi => oi.CatalogItemId != null 
                && context.TemplateItems.Any(ti => ti.CatalogItemId == oi.CatalogItemId && ti.TemplateId == templateId))
            .GroupBy(oi => oi.OrderId)
            .CountAsync();
        
        return new TemplateStatisticsDto
        {
            TemplateId = templateId,
            TemplateName = template.Title,
            TotalItems = totalItems,
            TotalQuantity = totalQuantity,
            OriginalTotalPrice = originalPrice,
            DiscountedPrice = template.AfterDiscount,
            DiscountPercentage = ((originalPrice - template.AfterDiscount) / originalPrice) * 100,
            OrderCount = orderCount,
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
            LastUpdatedAt = template.UpdatedAt
        };
    }
    
    public async Task<IEnumerable<TemplateUsageDto>> GetMostUsedTemplatesAsync(int topCount = 10)
    {
        var query = from template in context.Templates
                    join templateItem in context.TemplateItems on template.Id equals templateItem.TemplateId
                    join orderItem in context.OrderItems on templateItem.CatalogItemId equals orderItem.CatalogItemId
                    where !template.IsDeleted && template.IsActive
                    group template by new { template.Id, template.Title } into g
                    select new TemplateUsageDto
                    {
                        TemplateId = g.Key.Id,
                        TemplateName = g.Key.Title,
                        UsageCount = g.Count(),
                        TotalRevenue = g.Sum(t => t.AfterDiscount) // Approximate
                    };
        
        return await query
            .OrderByDescending(t => t.UsageCount)
            .Take(topCount)
            .ToListAsync();
    }
    
    // ========== Private Helper Methods ==========
    
    private async System.Threading.Tasks.Task RecalculateTemplatePricingAsync(int templateId)
    {
        var template = await GetTemplateByIdAsync(templateId);
        if (template == null)
            return;
        
        var totalPrice = await CalculateTemplateTotalPriceAsync(templateId);
        
        // Keep the same discount percentage
        var discountPercentage = template.BeforeDiscount > 0 
            ? (template.BeforeDiscount - template.AfterDiscount) / template.BeforeDiscount 
            : 0;
        
        template.BeforeDiscount = totalPrice;
        template.AfterDiscount = totalPrice * (1 - discountPercentage);
        template.UpdatedAt = DateTime.UtcNow;
        
        await context.SaveChangesAsync();
        
        logger.LogDebug("Template pricing recalculated: Template {TemplateId}, New Total {TotalPrice}", 
            templateId, totalPrice);
    }
}