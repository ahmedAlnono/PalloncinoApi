using Microsoft.EntityFrameworkCore;
using Palloncino.Data;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Services.Interfaces;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Palloncino.Models.DTOs;

namespace Palloncino.Services.Implementations;

public class QuotationService(
    ApplicationDbContext context,
    ILogger<QuotationService> logger,
    INotificationService notificationService) : IQuotationService
{
    // ========== CRUD Operations ==========
    
    public async Task<Quotation> CreateQuotationAsync(int orderId, List<CreateQuotationItemDto> items, string? notes = null, DateTime? validUntil = null)
    {
        var order = await context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
        
        if (order == null)
            throw new InvalidOperationException($"Order with ID {orderId} not found");
        
        if (order.Type != OrderType.Custom && order.Type != OrderType.Design)
            throw new InvalidOperationException("Quotations can only be created for custom or design orders");
        
        if (items == null || !items.Any())
            throw new InvalidOperationException("Quotation must contain at least one item");
        
        // Generate unique quotation number
        var quotationNumber = await GenerateQuotationNumberAsync();
        
        // Calculate total amount
        decimal totalAmount = 0;
        var quotationItems = new List<QuotationItem>();
        
        foreach (var itemDto in items)
        {
            var itemTotal = itemDto.Quantity * itemDto.UnitPrice;
            
            // Apply discount
            if (itemDto.DiscountAmount.HasValue && itemDto.DiscountAmount.Value > 0)
            {
                itemTotal -= itemDto.DiscountAmount.Value;
            }
            else if (itemDto.DiscountPercentage.HasValue && itemDto.DiscountPercentage.Value > 0)
            {
                itemTotal -= itemTotal * (itemDto.DiscountPercentage.Value / 100);
            }
            
            totalAmount += itemTotal;
            
            var quotationItem = new QuotationItem
            {
                ItemName = itemDto.ItemName,
                Description = itemDto.Description,
                Quantity = itemDto.Quantity,
                UnitPrice = itemDto.UnitPrice,
                DiscountPercentage = itemDto.DiscountPercentage,
                DiscountAmount = itemDto.DiscountAmount,
                IsRental = itemDto.IsRental,
                Notes = itemDto.Notes,
                CatalogItemId = itemDto.CatalogItemId,
                InventoryItemId = itemDto.InventoryItemId,
                DisplayOrder = quotationItems.Count + 1,
                CreatedAt = DateTime.UtcNow
            };
            
            quotationItems.Add(quotationItem);
        }
        
        var quotation = new Quotation
        {
            OrderId = orderId,
            QuotationNumber = quotationNumber,
            TotalAmount = totalAmount,
            Notes = notes,
            Status = QuotationStatus.Draft,
            ValidUntil = validUntil ?? DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        context.Quotations.Add(quotation);
        await context.SaveChangesAsync();
        
        // Add items
        foreach (var item in quotationItems)
        {
            item.QuotationId = quotation.Id;
            context.QuotationItems.Add(item);
        }
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Quotation created: {QuotationNumber} for Order {OrderId}", quotationNumber, orderId);
        
        return quotation;
    }
    
    public async Task<Quotation> UpdateQuotationAsync(int quotationId, List<UpdateQuotationItemDto> items, string? notes = null)
    {
        var quotation = await GetQuotationByIdAsync(quotationId);
        if (quotation == null)
            throw new InvalidOperationException($"Quotation with ID {quotationId} not found");
        
        if (!await CanModifyQuotationAsync(quotationId))
            throw new InvalidOperationException($"Quotation cannot be modified. Current status: {quotation.Status}");
        
        // Update notes
        if (notes != null)
            quotation.Notes = notes;
        
        quotation.UpdatedAt = DateTime.UtcNow;
        
        // Update items
        foreach (var itemDto in items)
        {
            var existingItem = quotation.QuotationItems.FirstOrDefault(i => i.Id == itemDto.Id);
            if (existingItem != null)
            {
                if (itemDto.ItemName != null)
                    existingItem.ItemName = itemDto.ItemName;
                if (itemDto.Description != null)
                    existingItem.Description = itemDto.Description;
                if (itemDto.Quantity.HasValue)
                    existingItem.Quantity = itemDto.Quantity.Value;
                if (itemDto.UnitPrice.HasValue)
                    existingItem.UnitPrice = itemDto.UnitPrice.Value;
                if (itemDto.DiscountPercentage.HasValue)
                    existingItem.DiscountPercentage = itemDto.DiscountPercentage.Value;
                if (itemDto.DiscountAmount.HasValue)
                    existingItem.DiscountAmount = itemDto.DiscountAmount.Value;
                if (itemDto.IsRental.HasValue)
                    existingItem.IsRental = itemDto.IsRental.Value;
                if (itemDto.Notes != null)
                    existingItem.Notes = itemDto.Notes;
                
                existingItem.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        // Recalculate total amount
        decimal totalAmount = 0;
        foreach (var item in quotation.QuotationItems)
        {
            var itemTotal = item.Quantity * item.UnitPrice;
            
            if (item.DiscountAmount.HasValue && item.DiscountAmount.Value > 0)
                itemTotal -= item.DiscountAmount.Value;
            else if (item.DiscountPercentage.HasValue && item.DiscountPercentage.Value > 0)
                itemTotal -= itemTotal * (item.DiscountPercentage.Value / 100);
            
            totalAmount += itemTotal;
        }
        
        quotation.TotalAmount = totalAmount;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Quotation updated: {QuotationNumber}", quotation.QuotationNumber);
        
        return quotation;
    }
    
    public async Task<bool> DeleteQuotationAsync(int quotationId)
    {
        var quotation = await GetQuotationByIdAsync(quotationId);
        if (quotation == null)
            return false;
        
        if (quotation.Status == QuotationStatus.Approved || quotation.Status == QuotationStatus.Converted)
            throw new InvalidOperationException($"Cannot delete {quotation.Status} quotation");
        
        // Remove items first
        var items = context.QuotationItems.Where(i => i.QuotationId == quotationId);
        context.QuotationItems.RemoveRange(items);
        
        context.Quotations.Remove(quotation);
        await context.SaveChangesAsync();
        
        logger.LogWarning("Quotation deleted: {QuotationNumber}", quotation.QuotationNumber);
        
        return true;
    }
    
    // ========== Queries ==========
    
    public async Task<Quotation?> GetQuotationByIdAsync(int quotationId)
    {
        return await context.Quotations
            .Include(q => q.Order)
                .ThenInclude(o => o != null ? o.Customer : null)
            .Include(q => q.QuotationItems)
            .FirstOrDefaultAsync(q => q.Id == quotationId && !q.IsDeleted);
    }
    
    public async Task<IEnumerable<Quotation>> GetQuotationsByOrderAsync(int orderId)
    {
        return await context.Quotations
            .Include(q => q.QuotationItems)
            .Where(q => q.OrderId == orderId && !q.IsDeleted)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<Quotation?> GetActiveQuotationByOrderAsync(int orderId)
    {
        return await context.Quotations
            .Include(q => q.QuotationItems)
            .Where(q => q.OrderId == orderId 
                && !q.IsDeleted 
                && q.Status != QuotationStatus.Expired 
                && q.Status != QuotationStatus.Rejected
                && q.Status != QuotationStatus.Converted)
            .OrderByDescending(q => q.CreatedAt)
            .FirstOrDefaultAsync();
    }
    
    // ========== PDF Generation ==========
    
    public async Task<byte[]> GenerateQuotationPdfAsync(int quotationId)
    {
        var quotation = await GetQuotationByIdAsync(quotationId);
        if (quotation == null)
            throw new InvalidOperationException($"Quotation with ID {quotationId} not found");
        
        var order = await context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == quotation.OrderId);
        
        if (order == null)
            throw new InvalidOperationException($"Order not found");
        
        var pdfData = new QuotationPdfDto
        {
            Quotation = quotation,
            Order = order,
            Customer = order.Customer,
            CompanyInfo = new CompanyInfoDto
            {
                Name = "Palloncino",
                Address = "123 Celebration Street, Riyadh, Saudi Arabia",
                Phone = "+966 5X XXX XXXX",
                Email = "info@palloncino.com",
                Website = "www.palloncino.com",
                TaxNumber = "1234567890"
            }
        };
        
        return GeneratePdfDocument(pdfData);
    }
    
    public async Task<string> GenerateQuotationHtmlAsync(int quotationId)
    {
        var quotation = await GetQuotationByIdAsync(quotationId);
        if (quotation == null)
            throw new InvalidOperationException($"Quotation with ID {quotationId} not found");
        
        var order = await context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == quotation.OrderId);
        
        var sb = new StringBuilder();
        
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset='UTF-8'>");
        sb.AppendLine("<title>Quotation " + quotation.QuotationNumber + "</title>");
        sb.AppendLine(@"<style>
            body { font-family: Arial, sans-serif; margin: 40px; }
            .header { text-align: center; margin-bottom: 30px; }
            .company-name { font-size: 28px; font-weight: bold; color: #ff6b9d; }
            .quotation-title { font-size: 24px; margin: 20px 0; }
            .info-table { width: 100%; margin-bottom: 30px; border-collapse: collapse; }
            .info-table td { padding: 8px; border: 1px solid #ddd; }
            .items-table { width: 100%; border-collapse: collapse; margin-bottom: 30px; }
            .items-table th { background-color: #ff6b9d; color: white; padding: 12px; text-align: left; }
            .items-table td { padding: 10px; border-bottom: 1px solid #ddd; }
            .total { text-align: right; font-size: 18px; font-weight: bold; margin-top: 20px; }
            .footer { margin-top: 50px; text-align: center; font-size: 12px; color: #888; }
            .valid-until { color: #ff6b9d; font-weight: bold; }
        </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        
        // Header
        sb.AppendLine("<div class='header'>");
        sb.AppendLine($"<div class='company-name'>Palloncino</div>");
        sb.AppendLine($"<div>Celebration & Party Planning</div>");
        sb.AppendLine("</div>");
        
        // Title
        sb.AppendLine($"<div class='quotation-title'>QUOTATION #{quotation.QuotationNumber}</div>");
        
        // Info Table
        sb.AppendLine("<table class='info-table'>");
        sb.AppendLine("<tr><td style='width:50%'><strong>Customer:</strong><br>" + (order.Customer?.FullName ?? "N/A") + "<br>" + (order.Customer?.Phone ?? "") + "<br>" + (order.Customer?.Email ?? "") + "</td>");
        sb.AppendLine($"<td><strong>Quotation Details:</strong><br>Date: {quotation.CreatedAt:yyyy-MM-dd}<br>Valid Until: <span class='valid-until'>{quotation.ValidUntil:yyyy-MM-dd}</span><br>Status: {quotation.Status}</td></tr>");
        sb.AppendLine("</table>");
        
        // Items Table
        sb.AppendLine("<table class='items-table'>");
        sb.AppendLine("<tr><th>#</th><th>Item</th><th>Description</th><th>Quantity</th><th>Unit Price</th><th>Discount</th><th>Total</th></tr>");
        
        int index = 1;
        foreach (var item in quotation.QuotationItems.OrderBy(i => i.DisplayOrder))
        {
            var discount = "";
            if (item.DiscountAmount.HasValue && item.DiscountAmount.Value > 0)
                discount = $"-{item.DiscountAmount.Value:C}";
            else if (item.DiscountPercentage.HasValue && item.DiscountPercentage.Value > 0)
                discount = $"-{item.DiscountPercentage.Value}%";
            
            var itemTotal = item.Quantity * item.UnitPrice;
            if (item.DiscountAmount.HasValue)
                itemTotal -= item.DiscountAmount.Value;
            else if (item.DiscountPercentage.HasValue)
                itemTotal -= itemTotal * (item.DiscountPercentage.Value / 100);
            
            sb.AppendLine($"<tr>");
            sb.AppendLine($"<td>{index++}</td>");
            sb.AppendLine($"<td>{item.ItemName}{(item.IsRental ? " (Rental)" : "")}</td>");
            sb.AppendLine($"<td>{item.Description ?? "-"}</td>");
            sb.AppendLine($"<td>{item.Quantity}</td>");
            sb.AppendLine($"<td>{item.UnitPrice:C}</td>");
            sb.AppendLine($"<td>{discount}</td>");
            sb.AppendLine($"<td>{itemTotal:C}</td>");
            sb.AppendLine($"</tr>");
        }
        
        sb.AppendLine("</table>");
        
        // Total
        sb.AppendLine("<div class='total'>");
        sb.AppendLine($"Total Amount: {quotation.TotalAmount:C}");
        sb.AppendLine("</div>");
        
        // Notes
        if (!string.IsNullOrEmpty(quotation.Notes))
        {
            sb.AppendLine("<div style='margin-top: 30px;'>");
            sb.AppendLine("<strong>Notes:</strong>");
            sb.AppendLine($"<p>{quotation.Notes}</p>");
            sb.AppendLine("</div>");
        }
        
        // Footer
        sb.AppendLine("<div class='footer'>");
        sb.AppendLine("Thank you for choosing Palloncino!<br>");
        sb.AppendLine("This is a computer-generated document and does not require a signature.");
        sb.AppendLine("</div>");
        
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        
        return sb.ToString();
    }
    
    // ========== Status Management ==========
    
    public async Task<Quotation> UpdateQuotationStatusAsync(int quotationId, QuotationStatus status, int updatedBy)
    {
        var quotation = await GetQuotationByIdAsync(quotationId);
        if (quotation == null)
            throw new InvalidOperationException($"Quotation with ID {quotationId} not found");
        
        if (!IsValidStatusTransition(quotation.Status, status))
            throw new InvalidOperationException($"Invalid status transition from {quotation.Status} to {status}");
        
        var oldStatus = quotation.Status;
        quotation.Status = status;
        quotation.UpdatedAt = DateTime.UtcNow;
        quotation.UpdatedBy = updatedBy;
        
        await context.SaveChangesAsync();
        
        logger.LogInformation("Quotation {QuotationNumber} status changed from {OldStatus} to {NewStatus}", 
            quotation.QuotationNumber, oldStatus, status);
        
        // Notify customer if quotation is ready
        if (status == QuotationStatus.Sent)
        {
            await notificationService.SendInternalNotificationAsync(
                quotation.Order.CustomerId, 
                "Quotation Ready", 
                $"Your quotation #{quotation.QuotationNumber} is ready for review.", 
                NotificationType.OrderUpdate, 
                quotationId, 
                "Quotation");
        }
        
        return quotation;
    }
    
    public async Task<Quotation> ApproveQuotationAsync(int quotationId, int approvedBy)
    {
        var quotation = await UpdateQuotationStatusAsync(quotationId, QuotationStatus.Approved, approvedBy);
        
        // Convert to order items
        var order = await context.Orders.FindAsync(quotation.OrderId);
        if (order != null)
        {
            // Clear existing order items
            var existingItems = context.OrderItems.Where(oi => oi.OrderId == order.Id);
            context.OrderItems.RemoveRange(existingItems);
            
            // Add quotation items as order items
            foreach (var item in quotation.QuotationItems)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    ItemName = item.ItemName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.Quantity * item.UnitPrice,
                    IsRental = item.IsRental,
                    CreatedAt = DateTime.UtcNow
                };
                
                context.OrderItems.Add(orderItem);
            }
            
            order.TotalAmount = quotation.TotalAmount;
            order.Status = OrderStatus.Approved;
            
            await context.SaveChangesAsync();
        }
        
        logger.LogInformation("Quotation {QuotationNumber} approved by User {ApprovedBy}", 
            quotation.QuotationNumber, approvedBy);
        
        return quotation;
    }
    
    public async Task<Quotation> RejectQuotationAsync(int quotationId, int rejectedBy)
    {
        return await UpdateQuotationStatusAsync(quotationId, QuotationStatus.Rejected, rejectedBy);
    }
    
    // ========== Validation ==========
    
    public async Task<bool> QuotationExistsAsync(int quotationId)
    {
        return await context.Quotations
            .AnyAsync(q => q.Id == quotationId && !q.IsDeleted);
    }
    
    public async Task<bool> CanModifyQuotationAsync(int quotationId)
    {
        var quotation = await GetQuotationByIdAsync(quotationId);
        if (quotation == null)
            return false;
        
        return quotation.Status == QuotationStatus.Draft;
    }
    
    // ========== Private Helper Methods ==========
    
    private async Task<string> GenerateQuotationNumberAsync()
    {
        var date = DateTime.UtcNow;
        var prefix = $"Q-{date:yyyyMMdd}";
        
        var lastQuotation = await context.Quotations
            .Where(q => q.QuotationNumber.StartsWith(prefix))
            .OrderByDescending(q => q.QuotationNumber)
            .FirstOrDefaultAsync();
        
        if (lastQuotation == null)
            return $"{prefix}-0001";
        
        var lastNumber = int.Parse(lastQuotation.QuotationNumber.Split('-').Last());
        var newNumber = lastNumber + 1;
        
        return $"{prefix}-{newNumber:D4}";
    }
    
    private bool IsValidStatusTransition(QuotationStatus from, QuotationStatus to)
    {
        return (from, to) switch
        {
            (QuotationStatus.Draft, QuotationStatus.Sent) => true,
            (QuotationStatus.Draft, QuotationStatus.Approved) => true,
            (QuotationStatus.Draft, QuotationStatus.Rejected) => true,
            (QuotationStatus.Sent, QuotationStatus.Approved) => true,
            (QuotationStatus.Sent, QuotationStatus.Rejected) => true,
            (QuotationStatus.Approved, QuotationStatus.Converted) => true,
            (_, QuotationStatus.Expired) when from != QuotationStatus.Converted => true,
            _ => false
        };
    }
    
    private byte[] GeneratePdfDocument(QuotationPdfDto data)
    {
        // QuestPDF requires license type
        QuestPDF.Settings.License = LicenseType.Community;
        
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));
                
                page.Header()
                    .ShowOnce()
                    .Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(data.CompanyInfo.Name).FontSize(20).Bold().FontColor("#ff6b9d");
                            col.Item().Text(data.CompanyInfo.Address).FontSize(9);
                            col.Item().Text($"Tel: {data.CompanyInfo.Phone} | Email: {data.CompanyInfo.Email}").FontSize(9);
                        });
                        
                        row.ConstantItem(150).Column(col =>
                        {
                            col.Item().Border(1).Padding(10).Column(borderCol =>
                            {
                                borderCol.Item().Text("QUOTATION").FontSize(14).Bold().AlignCenter();
                                borderCol.Item().Text($"#{data.Quotation.QuotationNumber}").FontSize(12).AlignCenter();
                            });
                        });
                    });
                
                page.Content().Column(col =>
                {
                    // Customer Info
                    col.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().Border(1).Padding(10).Column(customerCol =>
                        {
                            customerCol.Item().Text("BILL TO").FontSize(10).Bold();
                            customerCol.Item().Text(data.Customer.FullName).FontSize(10);
                            customerCol.Item().Text(data.Customer.Phone).FontSize(9);
                            customerCol.Item().Text(data.Customer.Email).FontSize(9);
                        });
                        
                        row.RelativeItem().Border(1).Padding(10).Column(detailsCol =>
                        {
                            detailsCol.Item().Text("QUOTATION DETAILS").FontSize(10).Bold();
                            detailsCol.Item().Text($"Date: {data.Quotation.CreatedAt:yyyy-MM-dd}").FontSize(9);
                            detailsCol.Item().Text($"Valid Until: {data.Quotation.ValidUntil:yyyy-MM-dd}").FontSize(9).Bold().FontColor("#ff6b9d");
                            detailsCol.Item().Text($"Status: {data.Quotation.Status}").FontSize(9);
                        });
                    });
                    
                    // Items Table
                    col.Item().PaddingTop(20).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(100);
                        });
                        
                        table.Header(header =>
                        {
                            header.Cell().Text("#").Bold();
                            header.Cell().Text("Item").Bold();
                            header.Cell().Text("Description").Bold();
                            header.Cell().Text("Qty").Bold().AlignRight();
                            header.Cell().Text("Unit Price").Bold().AlignRight();
                            header.Cell().Text("Discount").Bold().AlignRight();
                            header.Cell().Text("Total").Bold().AlignRight();
                        });
                        
                        int index = 1;
                        foreach (var item in data.Quotation.QuotationItems.OrderBy(i => i.DisplayOrder))
                        {
                            var itemTotal = item.Quantity * item.UnitPrice;
                            var discount = "";
                            
                            if (item.DiscountAmount.HasValue && item.DiscountAmount.Value > 0)
                            {
                                itemTotal -= item.DiscountAmount.Value;
                                discount = $"-{item.DiscountAmount.Value:C}";
                            }
                            else if (item.DiscountPercentage.HasValue && item.DiscountPercentage.Value > 0)
                            {
                                itemTotal -= itemTotal * (item.DiscountPercentage.Value / 100);
                                discount = $"-{item.DiscountPercentage.Value}%";
                            }
                            
                            table.Cell().Text(index.ToString());
                            table.Cell().Text(item.ItemName + (item.IsRental ? " (Rental)" : ""));
                            table.Cell().Text(item.Description ?? "-");
                            table.Cell().Text(item.Quantity.ToString()).AlignRight();
                            table.Cell().Text($"{item.UnitPrice:C}").AlignRight();
                            table.Cell().Text(discount).AlignRight();
                            table.Cell().Text($"{itemTotal:C}").AlignRight();
                            
                            index++;
                        }
                    });
                    
                    // Total
                    col.Item().PaddingTop(20).AlignRight().Column(totalCol =>
                    {
                        totalCol.Item().Text($"Subtotal: {data.Quotation.TotalAmount:C}").FontSize(10);
                        totalCol.Item().Text($"Total Amount: {data.Quotation.TotalAmount:C}").FontSize(14).Bold().FontColor("#ff6b9d");
                    });
                    
                    // Notes
                    if (!string.IsNullOrEmpty(data.Quotation.Notes))
                    {
                        col.Item().PaddingTop(20).Border(1).Padding(10).Column(notesCol =>
                        {
                            notesCol.Item().Text("NOTES").FontSize(10).Bold();
                            notesCol.Item().Text(data.Quotation.Notes).FontSize(9);
                        });
                    }
                });
                
                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Thank you for choosing Palloncino! ");
                        x.Span("This is a computer-generated document.").FontSize(8);
                    });
            });
        });
        
        return document.GeneratePdf();
    }
}