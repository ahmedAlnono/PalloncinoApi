namespace Palloncino.Services.Interfaces;

public interface IInventoryService
{
    Task ReserveInventoryAsync(int inventoryItemId, int quantity, string reason);
    Task ConsumeInventoryAsync(int inventoryItemId, int quantity, string reason);
    Task ReturnToInventoryAsync(int inventoryItemId, int quantity, string reason);
}