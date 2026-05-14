using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palloncino.Models.DTOs;
using Palloncino.Services.Interfaces;

namespace Palloncino.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController(
    IInventoryService inventoryService,
    IMapper mapper,
    ILogger<InventoryController> logger
) : ControllerBase
{
    [HttpGet("items")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetInventoryItems(
        [FromQuery] InventoryFilter filter,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10
    )
    {
        try
        {
            // Note: The service currently has GetInventoryItemsAsync(page, pageSize) 
            // and GetAllInventoryItemsAsync(filter). I'll use the filtered one for more flexibility.
            var items = await inventoryService.GetAllInventoryItemsAsync(filter);
            
            // Manual pagination for now if the service doesn't support it with filters
            var paginatedItems = items.Skip((page - 1) * pageSize).Take(pageSize);
            
            return Ok(mapper.Map<IEnumerable<InventoryItemDto>>(paginatedItems));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting inventory items");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("item/{id}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<InventoryItemDto>> GetInventoryItem(int id)
    {
        try
        {
            var item = await inventoryService.GetInventoryItemByIdAsync(id);
            if (item == null)
                return NotFound();

            return Ok(mapper.Map<InventoryItemDto>(item));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting inventory item {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("item/sku/{sku}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<InventoryItemDto>> GetInventoryItemBySku(string sku)
    {
        try
        {
            var item = await inventoryService.GetInventoryItemBySkuAsync(sku);
            if (item == null)
                return NotFound();

            return Ok(mapper.Map<InventoryItemDto>(item));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting inventory item by SKU {Sku}", sku);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("item")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<InventoryItemDto>> CreateInventoryItem(CreateInventoryItemDto dto)
    {
        try
        {
            var item = await inventoryService.CreateInventoryItemAsync(dto);
            return CreatedAtAction(nameof(GetInventoryItem), new { id = item.Id }, mapper.Map<InventoryItemDto>(item));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating inventory item");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("item/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UpdateInventoryItem(int id, [FromBody] UpdateInventoryItemDto dto)
    {
        try
        {
            bool isUpdated = await inventoryService.UpdateInventoryItemDto(dto, id);
            if (!isUpdated)
                return BadRequest(new { message = "Item not updated" });

            return Ok(new { message = "Item updated successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating inventory item {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("item/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteInventoryItem(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            bool isDeleted = await inventoryService.SoftDeleteInventoryItemAsync(id, userId);
            if (!isDeleted)
                return NotFound();

            return Ok(new { message = "Item deleted successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting inventory item {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("item/{id}/stock/add")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<InventoryItemDto>> AddStock(int id, [FromBody] UpdateInventoryStockDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var item = await inventoryService.AddStockAsync(id, dto.QuantityChange, dto.Reason ?? "Manual stock addition", userId);
            return Ok(mapper.Map<InventoryItemDto>(item));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding stock to item {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("item/{id}/stock/remove")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<InventoryItemDto>> RemoveStock(int id, [FromBody] UpdateInventoryStockDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var item = await inventoryService.RemoveStockAsync(id, Math.Abs(dto.QuantityChange), dto.Reason ?? "Manual stock removal", userId, dto.RelatedJobOrderId);
            return Ok(mapper.Map<InventoryItemDto>(item));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing stock from item {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("transfer")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<InventoryItemDto>> TransferStock([FromBody] TransferStockRequestDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var item = await inventoryService.TransferStockAsync(dto.InventoryItemId, dto.FromBranchId, dto.ToBranchId, dto.Quantity, userId);
            return Ok(mapper.Map<InventoryItemDto>(item));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error transferring stock");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("branch/{branchId}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetInventoryByBranch(int branchId)
    {
        try
        {
            var items = await inventoryService.GetInventoryItemsByBranchAsync(branchId);
            return Ok(mapper.Map<IEnumerable<InventoryItemDto>>(items));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting inventory for branch {BranchId}", branchId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("low-stock")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetLowStockItems([FromQuery] int? branchId)
    {
        try
        {
            if (!branchId.HasValue)
            {
                // If no branchId, get for all branches and filter
                var filter = new InventoryFilter { Status = Palloncino.Models.Enums.InventoryStatus.LowStock };
                var items = await inventoryService.GetAllInventoryItemsAsync(filter);
                return Ok(mapper.Map<IEnumerable<InventoryItemDto>>(items));
            }
            
            var branchItems = await inventoryService.GetLowStockItemsAsync(branchId.Value);
            return Ok(mapper.Map<IEnumerable<InventoryItemDto>>(branchItems));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting low stock items");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("out-of-stock")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<IEnumerable<InventoryItemDto>>> GetOutOfStockItems([FromQuery] int? branchId)
    {
        try
        {
            if (!branchId.HasValue)
            {
                var filter = new InventoryFilter { Status = Palloncino.Models.Enums.InventoryStatus.OutOfStock };
                var items = await inventoryService.GetAllInventoryItemsAsync(filter);
                return Ok(mapper.Map<IEnumerable<InventoryItemDto>>(items));
            }

            var branchItems = await inventoryService.GetOutOfStockItemsAsync(branchId.Value);
            return Ok(mapper.Map<IEnumerable<InventoryItemDto>>(branchItems));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting out of stock items");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("item/{id}/movements")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<IEnumerable<InventoryMovementDto>>> GetInventoryMovements(
        int id, 
        [FromQuery] DateTime? fromDate, 
        [FromQuery] DateTime? toDate
    )
    {
        try
        {
            var movements = await inventoryService.GetInventoryMovementsAsync(id, fromDate, toDate);
            return Ok(mapper.Map<IEnumerable<InventoryMovementDto>>(movements));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting movements for item {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("statistics")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<InventoryStatisticsDto>> GetInventoryStatistics([FromQuery] int? branchId)
    {
        try
        {
            var stats = await inventoryService.GetInventoryStatisticsAsync(branchId);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting inventory statistics");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("report")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<InventoryReportDto>> GetInventoryReport([FromQuery] int? branchId)
    {
        try
        {
            var report = await inventoryService.GenerateInventoryReportAsync(branchId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating inventory report");
            return BadRequest(new { message = ex.Message });
        }
    }

    private int GetCurrentUserId()
    {
        string? id = (User.Claims.FirstOrDefault(u => u.Type == "userId")?.Value)
            ?? (User.Claims.FirstOrDefault(u => u.Type == "sub")?.Value)
            ?? throw new Exception("User ID not found in token");
        return int.Parse(id);
    }
}

public class TransferStockRequestDto
{
    public int InventoryItemId { get; set; }
    public int FromBranchId { get; set; }
    public int ToBranchId { get; set; }
    public int Quantity { get; set; }
}