using Palloncino.Models.DTOs;
using Palloncino.Models.Entities;
using Task = System.Threading.Tasks.Task;
namespace Palloncino.Services.Interfaces;

public interface IInventoryService
{
    Task ReserveInventoryAsync(int inventoryItemId, int quantity, string reason);
    Task ConsumeInventoryAsync(int inventoryItemId, int quantity, string reason);
    Task ReturnToInventoryAsync(int inventoryItemId, int quantity, string reason);
    Task<IEnumerable<InventoryItemDto>> GetInventoryItemsAsync(int page, int pageSize);
    Task<InventoryItem> CreateInventoryItemAsync(CreateInventoryItemDto dto);
    Task<bool> UpdateInventoryItemDto(UpdateInventoryItemDto dto, int itemId);
}