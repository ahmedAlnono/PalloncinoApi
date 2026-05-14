using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palloncino.Models.DTOs;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;

namespace Palloncino.Controllers;

[ApiController]
[Route("api/quotations")]
[Authorize]
public class QuotationController : ControllerBase
{
    private readonly IQuotationService _quotationService;
    private readonly IOrderService _orderService;
    private readonly IMapper _mapper;
    
    public QuotationController(
        IQuotationService quotationService,
        IOrderService orderService,
        IMapper mapper)
    {
        _quotationService = quotationService;
        _orderService = orderService;
        _mapper = mapper;
    }
    
    /// <summary>
    /// POST /api/quotations - إنشاء عرض سعر مرتبط بطلب
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> CreateQuotation([FromBody] CreateQuotationRequest request)
    {
        // Verify order exists
        var order = await _orderService.GetOrderByIdAsync(request.OrderId);
        if (order == null)
            return NotFound(new { success = false, message = "Order not found" });
        
        if (order.Type != OrderType.Custom && order.Type != OrderType.Design)
            return BadRequest(new { success = false, message = "Quotations can only be created for custom or design orders" });
        
        try
        {
            var quotation = await _quotationService.CreateQuotationAsync(
                request.OrderId,
                request.Items,
                request.Notes,
                request.ValidUntil);
            
            var quotationDto = _mapper.Map<QuotationDto>(quotation);
            
            return Ok(new
            {
                success = true,
                message = "Quotation created successfully",
                data = quotationDto
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    /// <summary>
    /// GET /api/quotations/:id - عرض التفاصيل
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetQuotationById(int id)
    {
        var quotation = await _quotationService.GetQuotationByIdAsync(id);
        if (quotation == null)
            return NotFound(new { success = false, message = "Quotation not found" });
        
        // Check authorization
        var userId = GetCurrentUserId();
        var userRole = User.FindFirst("role")?.Value;
        
        if (userRole == "Customer" && quotation.Order.CustomerId != userId)
            return Forbid();
        
        var quotationDto = _mapper.Map<QuotationDto>(quotation);
        
        return Ok(new { success = true, data = quotationDto });
    }
    
    /// <summary>
    /// PUT /api/quotations/:id - تعديل البنود
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> UpdateQuotation(int id, [FromBody] UpdateQuotationRequest request)
    {
        try
        {
            var quotation = await _quotationService.UpdateQuotationAsync(id, request.Items, request.Notes);
            var quotationDto = _mapper.Map<QuotationDto>(quotation);
            
            return Ok(new
            {
                success = true,
                message = "Quotation updated successfully",
                data = quotationDto
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    /// <summary>
    /// GET /api/quotations/:id/pdf - إخراج PDF قابل للطباعة
    /// </summary>
    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> GenerateQuotationPdf(int id)
    {
        var quotation = await _quotationService.GetQuotationByIdAsync(id);
        if (quotation == null)
            return NotFound(new { success = false, message = "Quotation not found" });
        
        // Check authorization
        var userId = GetCurrentUserId();
        var userRole = User.FindFirst("role")?.Value;
        
        if (userRole == "Customer" && quotation.Order.CustomerId != userId)
            return Forbid();
        
        try
        {
            var pdfBytes = await _quotationService.GenerateQuotationPdfAsync(id);
            
            return File(pdfBytes, "application/pdf", $"Quotation_{quotation.QuotationNumber}.pdf");
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    /// <summary>
    /// GET /api/quotations/order/:orderId - عروض الأسعار لطلب محدد
    /// </summary>
    [HttpGet("order/{orderId}")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetQuotationsByOrder(int orderId)
    {
        var quotations = await _quotationService.GetQuotationsByOrderAsync(orderId);
        var quotationDtos = _mapper.Map<IEnumerable<QuotationDto>>(quotations);
        
        return Ok(new { success = true, data = quotationDtos });
    }
    
    /// <summary>
    /// PUT /api/quotations/:id/approve - اعتماد عرض السعر
    /// </summary>
    [HttpPut("{id}/approve")]
    [Authorize(Roles = "Customer,Admin")]
    public async Task<IActionResult> ApproveQuotation(int id)
    {
        try
        {
            var quotation = await _quotationService.ApproveQuotationAsync(id, GetCurrentUserId());
            var quotationDto = _mapper.Map<QuotationDto>(quotation);
            
            return Ok(new
            {
                success = true,
                message = "Quotation approved successfully",
                data = quotationDto
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    /// <summary>
    /// PUT /api/quotations/:id/reject - رفض عرض السعر
    /// </summary>
    [HttpPut("{id}/reject")]
    [Authorize(Roles = "Customer,Admin")]
    public async Task<IActionResult> RejectQuotation(int id)
    {
        try
        {
            var quotation = await _quotationService.RejectQuotationAsync(id, GetCurrentUserId());
            var quotationDto = _mapper.Map<QuotationDto>(quotation);
            
            return Ok(new
            {
                success = true,
                message = "Quotation rejected",
                data = quotationDto
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
    
    /// <summary>
    /// DELETE /api/quotations/:id - حذف عرض السعر
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteQuotation(int id)
    {
        try
        {
            var deleted = await _quotationService.DeleteQuotationAsync(id);
            
            if (!deleted)
                return NotFound(new { success = false, message = "Quotation not found" });
            
            return Ok(new { success = true, message = "Quotation deleted successfully" });
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

public class CreateQuotationRequest
{
    public int OrderId { get; set; }
    public List<CreateQuotationItemDto> Items { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime? ValidUntil { get; set; }
}

public class UpdateQuotationRequest
{
    public List<UpdateQuotationItemDto> Items { get; set; } = new();
    public string? Notes { get; set; }
}

