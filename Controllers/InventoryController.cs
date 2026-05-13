using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palloncino.Models.DTOs;
using Palloncino.Services.Interfaces;

namespace Palloncino.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController(
    IInventoryService inventoryService
) : ControllerBase
{
    [HttpGet("items")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetInventoryItems(
        [FromQuery] int page,
        [FromQuery] int pageSize
    )
    {
        try
        {
            var items = await inventoryService.GetInventoryItemsAsync(page, pageSize);
            return Ok(items);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [HttpPost("item")]
    public async Task<ActionResult> CreateInventoryItem(CreateInventoryItemDto dto)
    {
        try
        {
            var item = await inventoryService.CreateInventoryItemAsync(dto);
            return Ok(item);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("item/{id}/stock")]
    public async Task<ActionResult> UpdateInventoryItem(
        int id,
        [FromBody] UpdateInventoryItemDto dto
    )
    {
        try
        {
            bool isUpdated = await inventoryService.UpdateInventoryItemDto(dto,id);
            if(!isUpdated)
                return BadRequest("Item not Updated");
            return Ok("Item Updated");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    // private int GetCurrentUserId()
    // {
    //     string? id = (User.Claims.FirstOrDefault(u => u.Type == "sub")?.Value)
    //     ?? throw new Exception("Id not found");
    //     return int.Parse(id);
    // }
}