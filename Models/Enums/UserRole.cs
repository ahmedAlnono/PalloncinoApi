namespace Palloncino.Models.Enums;

public enum UserRole
{
    Customer = 1,      // External customer - can browse, order, track
    Admin = 2,         // Full system access - management
    Employee = 3,      // Internal staff - preparation and coordination
    Driver = 4,        // Delivery personnel - delivery tasks only
    Designer = 5       // Design team - custom design tasks
}


public enum Permission
{
    // Order Permissions
    ViewOrders,
    CreateOrder,
    UpdateOrder,
    DeleteOrder,
    ApproveOrder,
    RejectOrder,

    // Job Order Permissions
    ViewJobOrders,
    CreateJobOrder,
    UpdateJobOrder,
    DeleteJobOrder,
    AssignJobOrder,
    UpdateJobOrderStatus,
    SkipReturnPhase,

    // Task Permissions
    ViewTasks,
    CreateTask,
    UpdateTask,
    DeleteTask,
    AssignTask,
    CompleteTask,
    CompleteTaskForOthers, // BR-12

    // Task Management Permissions
    AssignTaskToOthers,
    CompleteAnyTask,
    SkipTask,

    // Catalog Permissions
    ViewCatalog,
    CreateCatalogItem,
    UpdateCatalogItem,
    DeleteCatalogItem,

    // Inventory Permissions
    ViewInventory,
    CreateInventoryItem,
    UpdateInventoryItem,
    DeleteInventoryItem,
    AdjustInventory,
    TransferInventory,

    // User Management Permissions
    ViewUsers,
    CreateUser,
    UpdateUser,
    DeleteUser,
    AssignRole,

    // Report Permissions
    ViewReports,
    ViewProfitReports,
    ViewPerformanceReports,
    ViewInventoryReports,
    ExportReports,

    // System Permissions
    ViewActivityLogs,
    ManageSettings,
    SendBroadcastNotifications,
    ManageBranches
}