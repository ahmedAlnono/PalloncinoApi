using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Services.Interfaces;

namespace Palloncino.Controllers;

[ApiController]
[Route("api/templates")]
public class TemplateController(
    ITemplateService templateService,
    IMapper mapper) : ControllerBase
{

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllTemplates([FromQuery] bool includeInactive = false)
    {
        var templates = await templateService.GetAllTemplatesAsync(includeInactive);
        var templateDtos = mapper.Map<IEnumerable<TemplateDto>>(templates);
        
        return Ok(templateDtos);
    }
    
    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActiveTemplates()
    {
        var templates = await templateService.GetActiveTemplatesAsync();
        var templateDtos = mapper.Map<IEnumerable<TemplateDto>>(templates);
        
        return Ok(templateDtos);
    }
    
    [HttpGet("category/{category}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTemplatesByCategory(string category)
    {
        var templates = await templateService.GetTemplatesByCategoryAsync(category);
        var templateDtos = mapper.Map<IEnumerable<TemplateDto>>(templates);
        
        return Ok(templateDtos);
    }
    
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTemplateById(int id)
    {
        var template = await templateService.GetTemplateByIdAsync(id);
        if (template == null)
            return NotFound(new { message = "Template not found" });
        
        var templateDto = mapper.Map<TemplateDto>(template);
        
        return Ok(templateDto);
    }
    
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateDto createDto)
    {
        var template = mapper.Map<Template>(createDto);
        var items = mapper.Map<List<TemplateItem>>(createDto.Items);
        
        try
        {
            var created = await templateService.CreateTemplateAsync(template, items);
            var templateDto = mapper.Map<TemplateDto>(created);
            
            return CreatedAtAction(nameof(GetTemplateById), new { id = templateDto.Id }, templateDto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] UpdateTemplateDto updateDto)
    {
        var existingTemplate = await templateService.GetTemplateByIdAsync(id);
        if (existingTemplate == null)
            return NotFound(new { message = "Template not found" });
        
        mapper.Map(updateDto, existingTemplate);
        
        List<TemplateItem>? items = null;
        if (updateDto.Items != null)
        {
            items = mapper.Map<List<TemplateItem>>(updateDto.Items);
        }
        
        try
        {
            var updated = await templateService.UpdateTemplateAsync(existingTemplate, items);
            var templateDto = mapper.Map<TemplateDto>(updated);
            
            return Ok(templateDto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        try
        {
            var deleted = await templateService.SoftDeleteTemplateAsync(id, GetCurrentUserId());
            
            if (!deleted)
                return NotFound(new { message = "Template not found" });
            
            return Ok(new { message = "Template deleted successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/activate")]
    public async Task<IActionResult> ActivateTemplate(int id)
    {
        try
        {
            var activated = await templateService.ActivateTemplateAsync(id);
            
            if (!activated)
                return NotFound(new { message = "Template not found" });
            
            return Ok(new { message = "Template activated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/deactivate")]
    public async Task<IActionResult> DeactivateTemplate(int id)
    {
        var deactivated = await templateService.DeactivateTemplateAsync(id);
        
        if (!deactivated)
            return NotFound(new { message = "Template not found" });
        
        return Ok(new { message = "Template deactivated successfully" });
    }
    
    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/duplicate")]
    public async Task<IActionResult> DuplicateTemplate(int id, [FromBody] DuplicateTemplateRequest request)
    {
        try
        {
            var duplicated = await templateService.DuplicateTemplateAsync(id, request.NewName, GetCurrentUserId());
            var templateDto = mapper.Map<TemplateDto>(duplicated);
            
            return Ok(templateDto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/items")]
    public async Task<IActionResult> AddTemplateItem(int id, [FromBody] AddTemplateItemRequest request)
    {
        try
        {
            var item = await templateService.AddTemplateItemAsync(id, request.CatalogItemId, request.Quantity);
            
            return Ok(new { message = "Item added successfully", item });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [Authorize(Roles = "Admin")]
    [HttpDelete("items/{itemId}")]
    public async Task<IActionResult> RemoveTemplateItem(int itemId)
    {
        var removed = await templateService.RemoveTemplateItemAsync(itemId);
        
        if (!removed)
            return NotFound(new { message = "Template item not found" });
        
        return Ok(new { message = "Item removed successfully" });
    }
    
    [Authorize(Roles = "Admin")]
    [HttpGet("{id}/statistics")]
    public async Task<IActionResult> GetTemplateStatistics(int id)
    {
        try
        {
            var statistics = await templateService.GetTemplateStatisticsAsync(id);
            return Ok(statistics);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
    
    [Authorize(Roles = "Admin")]
    [HttpGet("most-used")]
    public async Task<IActionResult> GetMostUsedTemplates([FromQuery] int topCount = 10)
    {
        var templates = await templateService.GetMostUsedTemplatesAsync(topCount);
        return Ok(templates);
    }
    
    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirst("userId")?.Value ?? "0");
    }
}

public class DuplicateTemplateRequest
{
    public string NewName { get; set; } = string.Empty;
}

public class AddTemplateItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; } = 1;
}