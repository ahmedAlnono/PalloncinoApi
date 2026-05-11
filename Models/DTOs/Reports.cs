using Palloncino.Models.Enums;

namespace Palloncino.Models.DTOs
{
    public class DateRangeDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
    
    public class ProfitReportDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalDamageDeductions { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal NetProfit { get; set; }
        public int TotalJobOrders { get; set; }
        public int CompletedJobOrders { get; set; }
        public int CancelledJobOrders { get; set; }
        public List<ProfitByBranchDto> ByBranch { get; set; } = new();
        public List<ProfitByDayDto> ByDay { get; set; } = new();
    }
    
    public class ProfitByBranchDto
    {
        public string BranchName { get; set; } = string.Empty;
        public int JobOrderCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit { get; set; }
    }
    
    public class ProfitByDayDto
    {
        public DateTime Date { get; set; }
        public int JobOrderCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
    }
    
    public class EmployeePerformanceReportDto
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public int AssignedTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int CompletedByOther { get; set; } // Tasks completed by others (BR-12)
        public int OverdueTasks { get; set; }
        public decimal CompletionRate { get; set; }
        public double AverageCompletionTimeHours { get; set; }
    }
    
    public class InventoryReportDto
    {
        public int TotalItems { get; set; }
        public int LowStockItems { get; set; }
        public int OutOfStockItems { get; set; }
        public decimal TotalStockValue { get; set; }
        public List<InventoryItemReportDto> Items { get; set; } = new();
    }
    
    public class InventoryItemReportDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinStockLevel { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public decimal StockValue { get; set; }
    }
    
    public class JobOrderReportDto
    {
        public int TotalJobOrders { get; set; }
        public int Completed { get; set; }
        public int InProgress { get; set; }
        public int Overdue { get; set; }
        public int Cancelled { get; set; }
        public decimal AverageCompletionTimeHours { get; set; }
        public List<JobOrderSummaryDto> JobOrders { get; set; } = new();
    }
    
    public class JobOrderSummaryDto
    {
        public int Id { get; set; }
        public string JobNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime DueAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public JobOrderStatus Status { get; set; }
        public ExecutionType ExecutionType { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit { get; set; }
    }
}