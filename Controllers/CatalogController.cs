using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Services.Interfaces;

namespace Palloncino.Controllers;

[ApiController]
[Route("api")]
public class CatalogController(
    ICatalogService catalogService,
    IMapper mapper) : ControllerBase
{

    // ========== Customer Endpoints (Public) ==========

    /// <summary>
    /// GET /api/catalog - عرض عناصر الكتالوج (للعميل)
    /// </summary>
    [HttpGet("catalog")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllCatalogItems([FromQuery] string? category, [FromQuery] bool includeOutOfStock = false)
    {
        var items = await catalogService.GetAllCatalogItemsAsync(category, includeOutOfStock);
        var itemDtos = mapper.Map<IEnumerable<CatalogItemDto>>(items);
        
        return Ok(new
        {
            success = true,
            count = itemDtos.Count(),
            data = itemDtos
        });
    }
    
    /// <summary>
    /// GET /api/catalog/:id - تفاصيل عنصر واحد
    /// </summary>
    [HttpGet("catalog/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCatalogItemById(int id)
    {
        var item = await catalogService.GetCatalogItemByIdAsync(id);
        
        if (item == null)
            return NotFound(new { success = false, message = "Catalog item not found" });
        
        var itemDto = mapper.Map<CatalogItemDto>(item);
        
        return Ok(new { success = true, data = itemDto });
    }
    
    /// <summary>
    /// GET /api/templates - باقات الحفلات الجاهزة (عيد ميلاد، خطوبة...)
    /// </summary>
    [HttpGet("templates")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllTemplates([FromQuery] string? category)
    {
        var templates = await catalogService.GetAllTemplatesAsync(category);
        var templateDtos = mapper.Map<IEnumerable<TemplateDto>>(templates);
        
        return Ok(new
        {
            success = true,
            count = templateDtos.Count(),
            data = templateDtos
        });
    }
    
    /// <summary>
    /// GET /api/templates/:id - تفاصيل باقة + السعر قبل/بعد الخصم
    /// </summary>
    [HttpGet("templates/{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTemplateById(int id)
    {
        var template = await catalogService.GetTemplateByIdAsync(id);
        
        if (template == null)
            return NotFound(new { success = false, message = "Template not found" });
        
        var discountedPrice = await catalogService.GetTemplateDiscountedPriceAsync(id);
        
        var response = new
        {
            success = true,
            data = new
            {
                id = template.Id,
                title = template.Title,
                description = template.Description,
                category = template.Category,
                beforeDiscount = template.BeforeDiscount,
                afterDiscount = template.AfterDiscount,
                discountPercentage = template.BeforeDiscount > 0 
                    ? Math.Round(((template.BeforeDiscount - template.AfterDiscount) / template.BeforeDiscount) * 100, 2)
                    : 0,
                imageUrl = template.ImageUrl,
                isActive = template.IsActive,
                items = template.TemplateItems?.Select(ti => new
                {
                    itemId = ti.CatalogItemId,
                    itemName = ti.CatalogItem?.Title,
                    itemPrice = ti.CatalogItem?.Price,
                    quantity = ti.Quantity,
                    totalPrice = (ti.CatalogItem?.Price ?? 0) * ti.Quantity
                }),
                createdAt = template.CreatedAt
            }
        };
        
        return Ok(response);
    }
    
    // ========== Admin Endpoints (Protected) ==========
    
    /// <summary>
    /// POST /api/catalog - إضافة عنصر كتالوج (Admin)
    /// </summary>
    [HttpPost("catalog")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCatalogItem([FromBody] CreateCatalogItemDto createDto)
    {
        var item = mapper.Map<CatalogItem>(createDto);
        
        try
        {
            var created = await catalogService.CreateCatalogItemAsync(item);
            var itemDto = mapper.Map<CatalogItemDto>(created);
            
            return CreatedAtAction(nameof(GetCatalogItemById), new { id = itemDto.Id }, new
            {
                success = true,
                message = "Catalog item created successfully",
                data = itemDto
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    /// <summary>
    /// PUT /api/catalog/:id - تعديل عنصر
    /// </summary>
    [HttpPut("catalog/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCatalogItem(int id, [FromBody] UpdateCatalogItemDto updateDto)
    {
        var existingItem = await catalogService.GetCatalogItemByIdAsync(id);
        if (existingItem == null)
            return NotFound(new { success = false, message = "Catalog item not found" });
        
        mapper.Map(updateDto, existingItem);
        existingItem.UpdatedBy = GetCurrentUserId();
        
        try
        {
            var updated = await catalogService.UpdateCatalogItemAsync(existingItem);
            var itemDto = mapper.Map<CatalogItemDto>(updated);
            
            return Ok(new
            {
                success = true,
                message = "Catalog item updated successfully",
                data = itemDto
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    /// <summary>
    /// DELETE /api/catalog/:id - حذف عنصر
    /// </summary>
    [HttpDelete("catalog/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCatalogItem(int id)
    {
        var exists = await catalogService.CatalogItemExistsAsync(id);
        if (!exists)
            return NotFound(new { success = false, message = "Catalog item not found" });
        
        try
        {
            var deleted = await catalogService.SoftDeleteCatalogItemAsync(id, GetCurrentUserId());
            
            if (!deleted)
                return BadRequest(new { success = false, message = "Failed to delete catalog item" });
            
            return Ok(new
            {
                success = true,
                message = "Catalog item deleted successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirst("userId")?.Value ?? "0");
    }
}