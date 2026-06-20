namespace Palloncino.Models.DTOs;

public class BranchStatisticsDto
{
    public int BranchId {get;set;}
    public string BranchName {get;set;} ="";
    public int EmployeeCount  {get;set;}
    public int ActiveJobOrdersCount {get;set;}
    public int CompletedJobOrdersThisMonth {get;set;}
    public decimal TotalRevenueThisMonth {get;set;}
}