using AutoMapper;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;
using Palloncino.Models.DTOs;

namespace Palloncino.Mappers;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        // ========== User & Auth Mappings ==========
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.BranchName, 
                opt => opt.MapFrom(src => src.Branch != null ? src.Branch.Name : null))
            .ForMember(dest => dest.ProfileImageUrl, 
                opt => opt.MapFrom(src => src.ProfileImageUrl))
            .ReverseMap();
        
        CreateMap<User, UserListDto>()
            .ForMember(dest => dest.BranchName, 
                opt => opt.MapFrom(src => src.Branch != null ? src.Branch.Name : null));
        
        CreateMap<RegisterRequestDto, User>()
            .ForMember(dest => dest.PasswordHash, 
                opt => opt.MapFrom(src => src.Password))
            .ForMember(dest => dest.Role, 
                opt => opt.MapFrom(src => UserRole.Customer))
            .ForMember(dest => dest.Status, 
                opt => opt.MapFrom(src => UserStatus.Active))
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.IsActive, 
                opt => opt.MapFrom(src => true))
            .ForMember(dest => dest.IsDeleted, 
                opt => opt.MapFrom(src => false));
        
        CreateMap<CreateUserDto, User>()
            .ForMember(dest => dest.PasswordHash, 
                opt => opt.MapFrom(src => src.Password))
            .ForMember(dest => dest.Status, 
                opt => opt.MapFrom(src => UserStatus.Active))
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow));
        
        CreateMap<UpdateUserDto, User>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        
        // ========== Branch Mappings ==========
        CreateMap<Branch, BranchDto>()
            .ForMember(dest => dest.EmployeeCount, 
                opt => opt.MapFrom(src => src.Users != null ? src.Users.Count(u => u.Role != UserRole.Customer) : 0));
        
        CreateMap<CreateBranchDto, Branch>()
            .ForMember(dest => dest.Status, 
                opt => opt.MapFrom(src => BranchStatus.Active))
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow));
        
        CreateMap<UpdateBranchDto, Branch>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        
        // ========== Catalog Mappings ==========
        CreateMap<CatalogItem, CatalogItemDto>().ReverseMap();
        
        CreateMap<CreateCatalogItemDto, CatalogItem>()
            .ForMember(dest => dest.Status, 
                opt => opt.MapFrom(src => ItemStatus.Available))
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest=> dest.IsActive,
                opt=> opt.MapFrom(src=>true));
        
        CreateMap<UpdateCatalogItemDto, CatalogItem>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        
        // Template Mappings
        CreateMap<Template, TemplateDto>()
            .ForMember(dest => dest.DiscountPercentage, 
                opt => opt.MapFrom(src => src.BeforeDiscount > 0 
                    ? ((src.BeforeDiscount - src.AfterDiscount) / src.BeforeDiscount) * 100 
                    : 0))
            .ForMember(dest => dest.Items, 
                opt => opt.MapFrom(src => src.TemplateItems));
        
        CreateMap<TemplateItem, TemplateItemDto>()
            .ForMember(dest => dest.ItemTitle, 
                opt => opt.MapFrom(src => src.CatalogItem != null ? src.CatalogItem.Title : null))
            .ForMember(dest => dest.ItemImageUrl, 
                opt => opt.MapFrom(src => src.CatalogItem != null ? src.CatalogItem.ImageUrl : null))
            .ForMember(dest => dest.UnitPrice, 
                opt => opt.MapFrom(src => src.CatalogItem != null ? src.CatalogItem.Price : 0))
            .ForMember(dest => dest.TotalPrice, 
                opt => opt.MapFrom(src => (src.CatalogItem != null ? src.CatalogItem.Price : 0) * src.Quantity));
        
        CreateMap<CreateTemplateDto, Template>()
            .ForMember(dest => dest.IsActive, 
                opt => opt.MapFrom(src => true))
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow));
        
        CreateMap<CreateTemplateItemDto, TemplateItem>();
        
        // ========== Inventory Mappings ==========
        CreateMap<InventoryItem, InventoryItemDto>()
            .ForMember(dest => dest.BranchName, 
                opt => opt.MapFrom(src => src.Branch != null ? src.Branch.Name : null))
            .ForMember(dest => dest.IsLowStock, 
                opt => opt.MapFrom(src => src.MinStockLevel.HasValue && src.Quantity <= src.MinStockLevel.Value))
            .ForMember(dest => dest.Status, 
                opt => opt.MapFrom(src => src.Quantity <= 0 ? InventoryStatus.OutOfStock 
                    : (src.MinStockLevel.HasValue && src.Quantity <= src.MinStockLevel.Value ? InventoryStatus.LowStock 
                    : InventoryStatus.InStock)));
        
        CreateMap<CreateInventoryItemDto, InventoryItem>()
            .ForMember(dest => dest.Status, 
                opt => opt.MapFrom(src => InventoryStatus.InStock))
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow));
        
        CreateMap<UpdateInventoryItemDto, InventoryItem>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        
        CreateMap<InventoryMovement, InventoryMovementDto>()
            .ForMember(dest => dest.ItemName, 
                opt => opt.MapFrom(src => src.InventoryItem != null ? src.InventoryItem.Title : null))
            .ForMember(dest => dest.PerformedByName, 
                opt => opt.MapFrom(src => src.Performer != null ? src.Performer.FullName : null));
        
        // ========== Order Mappings ==========
        CreateMap<Order, OrderDto>()
            .ForMember(dest => dest.CustomerName, 
                opt => opt.MapFrom(src => src.Customer != null ? src.Customer.FullName : null))
            .ForMember(dest => dest.CustomerPhone, 
                opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Phone : null))
            .ForMember(dest => dest.Items, 
                opt => opt.MapFrom(src => src.OrderItems))
            .ForMember(dest => dest.Attachments, 
                opt => opt.MapFrom(src => src.Attachments))
            .ForMember(dest => dest.JobOrderId, 
                opt => opt.MapFrom(src => src.JobOrder != null ? src.JobOrder.Id : (int?)null));
        
        CreateMap<OrderItem, OrderItemDto>()
            .ForMember(dest => dest.CatalogItemId, 
                opt => opt.MapFrom(src => src.CatalogItemId))
            .ForMember(dest => dest.ItemName, 
                opt => opt.MapFrom(src => src.ItemName ?? (src.CatalogItem != null ? src.CatalogItem.Title : null)))
            .ForMember(dest => dest.TotalPrice, 
                opt => opt.MapFrom(src => src.Quantity * src.UnitPrice));
        
        CreateMap<CreateOrderDto, Order>()
            .ForMember(dest => dest.Status, 
                opt => opt.MapFrom(src => OrderStatus.PendingReview))
            .ForMember(dest => dest.Source, 
                opt => opt.MapFrom(src => OrderSource.MobileApp))
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.TotalAmount, 
                opt => opt.Ignore()); // Calculated from items
        
        CreateMap<CreateOrderItemDto, OrderItem>()
            .ForMember(dest => dest.TotalPrice, 
                opt => opt.MapFrom(src => src.Quantity * (src.UnitPrice ?? 0)));
        
        // ========== Quotation Mappings ==========
        CreateMap<Quotation, QuotationDto>()
            .ForMember(dest => dest.Items, 
                opt => opt.MapFrom(src => src.QuotationItems));
        
        CreateMap<QuotationItem, QuotationItemDto>()
            .ForMember(dest => dest.FinalPrice, 
                opt => opt.MapFrom(src => 
                    src.DiscountAmount.HasValue && src.DiscountAmount.Value > 0 
                        ? src.TotalPrice - src.DiscountAmount.Value
                        : (src.DiscountPercentage.HasValue && src.DiscountPercentage.Value > 0
                            ? src.TotalPrice - (src.TotalPrice * src.DiscountPercentage.Value / 100)
                            : src.TotalPrice)))
            .ForMember(dest => dest.TotalPrice, 
                opt => opt.MapFrom(src => src.Quantity * src.UnitPrice));
        
        CreateMap<CreateQuotationDto, Quotation>()
            .ForMember(dest => dest.QuotationNumber, 
                opt => opt.MapFrom(src => GenerateQuotationNumber()))
            .ForMember(dest => dest.Status, 
                opt => opt.MapFrom(src => QuotationStatus.Draft))
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.ValidUntil, 
                opt => opt.MapFrom(src => src.ValidUntil ?? DateTime.UtcNow.AddDays(7)));
        
        CreateMap<CreateQuotationItemDto, QuotationItem>();
        
        // ========== JobOrder Mappings ==========
        CreateMap<JobOrder, JobOrderDto>()
            .ForMember(dest => dest.JobNumber, 
                opt => opt.MapFrom(src => src.JobNumber))
            .ForMember(dest => dest.OrderNumber, 
                opt => opt.MapFrom(src => src.SourceOrder != null ? src.SourceOrder.Id.ToString() : null))
            .ForMember(dest => dest.BranchName, 
                opt => opt.MapFrom(src => src.Branch != null ? src.Branch.Name : null))
            .ForMember(dest => dest.CoordinatorName, 
                opt => opt.MapFrom(src => src.Coordinator != null ? src.Coordinator.FullName : null))
            .ForMember(dest => dest.Tasks, 
                opt => opt.MapFrom(src => src.Tasks))
            .ForMember(dest => dest.JobOrderItems, 
                opt => opt.MapFrom(src => src.JobOrderItems))
            .ForMember(dest => dest.CountdownSeconds, 
                opt => opt.MapFrom(src => (src.DueAt - DateTime.UtcNow).TotalSeconds))
            .ForMember(dest => dest.CountdownDisplay, 
                opt => opt.MapFrom(src => FormatCountdown((src.DueAt - DateTime.UtcNow).TotalSeconds)))
            .ForMember(dest => dest.Profit, 
                opt => opt.MapFrom(src => src.TotalRevenue - src.TotalCost));
        
        CreateMap<JobOrder, JobOrderListDto>()
            .ForMember(dest => dest.JobNumber, 
                opt => opt.MapFrom(src => src.JobNumber))
            .ForMember(dest => dest.BranchName, 
                opt => opt.MapFrom(src => src.Branch != null ? src.Branch.Name : null))
            .ForMember(dest => dest.CoordinatorName, 
                opt => opt.MapFrom(src => src.Coordinator != null ? src.Coordinator.FullName : null))
            .ForMember(dest => dest.CountdownSeconds, 
                opt => opt.MapFrom(src => (src.DueAt - DateTime.UtcNow).TotalSeconds))
            .ForMember(dest => dest.CountdownDisplay, 
                opt => opt.MapFrom(src => FormatCountdown((src.DueAt - DateTime.UtcNow).TotalSeconds)))
            .ForMember(dest => dest.TaskCount, 
                opt => opt.MapFrom(src => src.Tasks != null ? src.Tasks.Count : 0))
            .ForMember(dest => dest.CompletedTaskCount, 
                opt => opt.MapFrom(src => src.Tasks!= null? src.Tasks.Count(t => t.Status == Models.Enums.TaskStatus.Completed) : 0));
        
        CreateMap<CreateJobOrderDto, JobOrder>()
            .ForMember(dest => dest.JobNumber, 
                opt => opt.MapFrom(src => GenerateJobNumber()))
            .ForMember(dest => dest.Status, 
                opt => opt.MapFrom(src => JobOrderStatus.Pending))
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.TotalCost, 
                opt => opt.MapFrom(src => 0m))
            .ForMember(dest => dest.TotalRevenue, 
                opt => opt.MapFrom(src => 0m));
        
        CreateMap<UpdateJobOrderDto, JobOrder>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        
        // ========== JobOrderItem Mappings ==========
        CreateMap<JobOrderItem, JobOrderItemDto>()
            .ForMember(dest => dest.TotalCost, 
                opt => opt.MapFrom(src => src.QuantityUsed * src.CostPerUnit))
            .ForMember(dest => dest.TotalSellingPrice, 
                opt => opt.MapFrom(src => src.SellingPricePerUnit.HasValue 
                    ? src.QuantityUsed * src.SellingPricePerUnit.Value 
                    : (decimal?)null))
            .ForMember(dest => dest.PreparedByName, 
                opt => opt.MapFrom(src => src.PreparedByUser != null ? src.PreparedByUser.FullName : null));
        
        CreateMap<AddJobOrderItemDto, JobOrderItem>()
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.Status, 
                opt => opt.MapFrom(src => JobOrderItemStatus.Pending));
        
        // ========== Task Mappings ==========
        CreateMap<Models.Entities.Task, TaskDto>()
            .ForMember(dest => dest.JobNumber, 
                opt => opt.MapFrom(src => src.JobOrder != null ? src.JobOrder.JobNumber : null))
            .ForMember(dest => dest.AssignedToName, 
                opt => opt.MapFrom(src => src.Assignee != null ? src.Assignee.FullName : null))
            .ForMember(dest => dest.CompletedByName, 
                opt => opt.MapFrom(src => src.Completer != null ? src.Completer.FullName : null))
            .ForMember(dest => dest.SubTasks, 
                opt => opt.MapFrom(src => src.SubTasks))
            .ForMember(dest => dest.ChecklistItems, 
                opt => opt.MapFrom(src => src.ChecklistItems))
            .ForMember(dest => dest.CountdownSeconds, 
                opt => opt.MapFrom(src => (src.DueAt - DateTime.UtcNow).TotalSeconds));
        
        CreateMap<SubTask, SubTaskDto>()
            .ForMember(dest => dest.CompletedByName, 
                opt => opt.MapFrom(src => src.Completer != null ? src.Completer.FullName : null));
        
        CreateMap<CreateTaskDto, Models.Entities.Task>()
            .ForMember(dest => dest.Status, 
                opt => opt.MapFrom(src => Models.Enums.TaskStatus.Pending))
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow));
        
        CreateMap<CreateSubTaskDto, SubTask>()
            .ForMember(dest => dest.IsCompleted, 
                opt => opt.MapFrom(src => false));
        
        // ========== Checklist Mappings ==========
        CreateMap<ChecklistItem, ChecklistItemDto>()
            .ForMember(dest => dest.CheckedByName, 
                opt => opt.MapFrom(src => src.Checker != null ? src.Checker.FullName : null));
        
        // ========== Notification Mappings ==========
        CreateMap<Notification, NotificationDto>();
        
        CreateMap<SendNotificationDto, Notification>()
            .ForMember(dest => dest.IsRead, 
                opt => opt.MapFrom(src => false))
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.SentAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow));
        
        // ========== Chat Mappings ==========
        CreateMap<ChatMessage, ChatMessageDto>()
            .ForMember(dest => dest.SenderName, 
                opt => opt.MapFrom(src => src.Sender != null ? src.Sender.FullName : null))
            .ForMember(dest => dest.SenderImageUrl, 
                opt => opt.MapFrom(src => src.Sender != null ? src.Sender.ProfileImageUrl : null));
        
        CreateMap<SendChatMessageDto, ChatMessage>()
            .ForMember(dest => dest.IsRead, 
                opt => opt.MapFrom(src => false))
            .ForMember(dest => dest.CreatedAt, 
                opt => opt.MapFrom(src => DateTime.UtcNow));
        
        // ========== Attachment Mappings ==========
        CreateMap<Attachment, AttachmentDto>()
            .ForMember(dest => dest.UploadedByName, 
                opt => opt.MapFrom(src => src.Uploader != null ? src.Uploader.FullName : null));
        
        // ========== Activity Log Mappings ==========
        CreateMap<ActivityLog, ActivityLogDto>()
            .ForMember(dest => dest.UserName, 
                opt => opt.MapFrom(src => src.User != null ? src.User.FullName : null));
        
        // ========== Report Mappings ==========
        CreateMap<JobOrder, JobOrderSummaryDto>()
            .ForMember(dest => dest.JobNumber, 
                opt => opt.MapFrom(src => src.JobNumber))
            .ForMember(dest => dest.BranchName, 
                opt => opt.MapFrom(src => src.Branch != null ? src.Branch.Name : null))
            .ForMember(dest => dest.CustomerName, 
                opt => opt.MapFrom(src => src.SourceOrder != null && src.SourceOrder.Customer != null 
                    ? src.SourceOrder.Customer.FullName : null))
            .ForMember(dest => dest.Revenue, 
                opt => opt.MapFrom(src => src.TotalRevenue))
            .ForMember(dest => dest.Cost, 
                opt => opt.MapFrom(src => src.TotalCost))
            .ForMember(dest => dest.Profit, 
                opt => opt.MapFrom(src => src.TotalRevenue - src.TotalCost))
            .ForMember(dest => dest.CompletedAt, 
                opt => opt.MapFrom(src => src.Status == JobOrderStatus.Completed ? src.UpdatedAt : null));
    }
    
    // ========== Helper Methods ==========
    
    private static string GenerateQuotationNumber()
    {
        return $"Q-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
    }
    
    private static string GenerateJobNumber()
    {
        return $"JO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
    }
    
    private static string FormatCountdown(double totalSeconds)
    {
        if (totalSeconds <= 0)
            return "Overdue";
        
        var timeSpan = TimeSpan.FromSeconds(totalSeconds);
        
        if (timeSpan.TotalHours >= 24)
            return $"{timeSpan.Days}d {timeSpan.Hours}h";
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        
        return $"{timeSpan.Seconds}s";
    }
}