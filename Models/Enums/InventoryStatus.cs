namespace Palloncino.Models.Enums;

public enum InventoryStatus
{
    InStock = 1,
    LowStock = 2,    // Below minimum threshold
    OutOfStock = 3,
    Damaged = 4,
    Returned = 5
}