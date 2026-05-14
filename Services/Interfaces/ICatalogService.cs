using Palloncino.Models.Entities;
using Palloncino.Models.Enums;

namespace Palloncino.Services.Interfaces;

public interface ICatalogService
{
    // ========== Customer Endpoints ==========
    Task<IEnumerable<CatalogItem>> GetAllCatalogItemsAsync(string? category = null, bool includeOutOfStock = false);
    Task<CatalogItem?> GetCatalogItemByIdAsync(int id);
    Task<IEnumerable<Template>> GetAllTemplatesAsync(string? category = null);
    Task<Template?> GetTemplateByIdAsync(int id);
    
    // ========== Admin Endpoints ==========
    Task<CatalogItem> CreateCatalogItemAsync(CatalogItem item);
    Task<CatalogItem> UpdateCatalogItemAsync(CatalogItem item);
    Task<bool> DeleteCatalogItemAsync(int id);
    Task<bool> SoftDeleteCatalogItemAsync(int id, int deletedBy);
    
    // ========== Helper Methods ==========
    Task<bool> CatalogItemExistsAsync(int id);
    Task<bool> SkuExistsAsync(string sku, int? excludeId = null);
    Task<decimal> GetTemplateDiscountedPriceAsync(int templateId);
}