namespace Palloncino.Models.Enums;

public enum MovementType
{
    StockIn = 1,       // Adding to inventory (purchase)
    StockOut = 2,      // Removing from inventory (used in job order)
    Return = 3,        // Returning to inventory (rental return)
    Adjustment = 4,    // Manual adjustment (damage, loss)
    Transfer = 5       // Transfer between branches
}