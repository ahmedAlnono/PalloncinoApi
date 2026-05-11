namespace Palloncino.Models.Enums;

public enum UserRole
{
    Customer = 1,      // External customer - can browse, order, track
    Admin = 2,         // Full system access - management
    Employee = 3,      // Internal staff - preparation and coordination
    Driver = 4,        // Delivery personnel - delivery tasks only
    Designer = 5       // Design team - custom design tasks
}