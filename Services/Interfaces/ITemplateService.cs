using Palloncino.Models.Entities;
using Palloncino.Models.DTOs;
namespace Palloncino.Services.Interfaces;

public interface ITemplateService
{
    // CRUD Operations
    Task<Template> CreateTemplateAsync(Template template, List<TemplateItem> items);
    Task<Template> UpdateTemplateAsync(Template template, List<TemplateItem>? items = null);
    Task<bool> DeleteTemplateAsync(int templateId);
    Task<bool> SoftDeleteTemplateAsync(int templateId, int deletedBy);
    
    // Queries
    Task<Template?> GetTemplateByIdAsync(int templateId);
    Task<Template?> GetTemplateByNameAsync(string name);
    Task<IEnumerable<Template>> GetAllTemplatesAsync(bool includeInactive = false);
    Task<IEnumerable<Template>> GetActiveTemplatesAsync();
    Task<IEnumerable<Template>> GetTemplatesByCategoryAsync(string category);
    
    // Template Items Management
    Task<TemplateItem> AddTemplateItemAsync(int templateId, int catalogItemId, int quantity);
    Task<bool> RemoveTemplateItemAsync(int templateItemId);
    Task<TemplateItem> UpdateTemplateItemQuantityAsync(int templateItemId, int quantity);
    Task<IEnumerable<TemplateItem>> GetTemplateItemsAsync(int templateId);
    
    // Validation
    Task<bool> TemplateExistsAsync(int templateId);
    Task<bool> TemplateNameExistsAsync(string name, int? excludeTemplateId = null);
    
    // Business Logic
    Task<decimal> CalculateTemplateTotalPriceAsync(int templateId);
    Task<decimal> CalculateTemplateDiscountPercentageAsync(int templateId);
    Task<Template> DuplicateTemplateAsync(int templateId, string newName, int duplicatedBy);
    
    // Status Management
    Task<bool> ActivateTemplateAsync(int templateId);
    Task<bool> DeactivateTemplateAsync(int templateId);
    
    // Bulk Operations
    Task<int> BulkUpdateTemplatePricesAsync(decimal percentageIncrease);
    Task<int> BulkDeactivateExpiredTemplatesAsync(int daysSinceLastUpdate);
    
    // Statistics
    Task<TemplateStatisticsDto> GetTemplateStatisticsAsync(int templateId);
    Task<IEnumerable<TemplateUsageDto>> GetMostUsedTemplatesAsync(int topCount = 10);
}