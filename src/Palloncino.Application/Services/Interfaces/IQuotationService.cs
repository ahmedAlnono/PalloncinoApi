using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;

namespace Palloncino.Services.Interfaces;

public interface IQuotationService
{
    // ========== CRUD Operations ==========
    Task<Quotation> CreateQuotationAsync(int orderId, List<CreateQuotationItemDto> items, string? notes = null, DateTime? validUntil = null);
    Task<Quotation> UpdateQuotationAsync(int quotationId, List<UpdateQuotationItemDto> items, string? notes = null);
    Task<bool> DeleteQuotationAsync(int quotationId);
    
    // ========== Queries ==========
    Task<Quotation?> GetQuotationByIdAsync(int quotationId);
    Task<IEnumerable<Quotation>> GetQuotationsByOrderAsync(int orderId);
    Task<Quotation?> GetActiveQuotationByOrderAsync(int orderId);
    
    // ========== PDF Generation ==========
    Task<byte[]> GenerateQuotationPdfAsync(int quotationId);
    Task<string> GenerateQuotationHtmlAsync(int quotationId);
    
    // ========== Status Management ==========
    Task<Quotation> UpdateQuotationStatusAsync(int quotationId, QuotationStatus status, int updatedBy);
    Task<Quotation> ApproveQuotationAsync(int quotationId, int approvedBy);
    Task<Quotation> RejectQuotationAsync(int quotationId, int rejectedBy);
    
    // ========== Validation ==========
    Task<bool> QuotationExistsAsync(int quotationId);
    Task<bool> CanModifyQuotationAsync(int quotationId);
}