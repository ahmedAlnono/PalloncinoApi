namespace Palloncino.Models.DTOs
{
    // ========== Base Classes ==========
    
    public abstract class BaseDto
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
    }
    
    public class ApiResponse<T>
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = "";
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
        public int StatusCode { get; set; } = 200;
    }
    
    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPrevious => PageNumber > 1;
        public bool HasNext => PageNumber < TotalPages;
    }
    
    public class PagingParameters
    {
        private const int MaxPageSize = 100;
        private int _pageSize = 10;
        
        public int PageNumber { get; set; } = 1;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
        }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = false;
    }
}