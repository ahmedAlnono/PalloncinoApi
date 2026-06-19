using Palloncino.Models.Enums;

namespace Palloncino.Core.Constants;

public static class RolePermissions
{
    public static readonly Dictionary<UserRole, List<string>> Map = new()
    {
        // ========== Customer ==========
        [UserRole.Customer] = new List<string>
        {
            // Orders
            Permissions.OrdersView,
            Permissions.OrdersCreate,
            Permissions.OrdersUpdate,
            // Catalog
            Permissions.CatalogView,
            // Quotations
            Permissions.QuotationsView,
            // Designs
            Permissions.DesignsView,
            Permissions.DesignsCreate,
            Permissions.DesignsUpdate,
        },

        // ========== Employee ==========
        [UserRole.Employee] = new List<string>
        {
            // Orders
            Permissions.OrdersView,
            Permissions.OrdersCreate,
            Permissions.OrdersUpdate,
            Permissions.OrdersApprove,
            Permissions.OrdersReject,
            // Job Orders
            Permissions.JobOrdersView,
            Permissions.JobOrdersCreate,
            Permissions.JobOrdersUpdate,
            Permissions.JobOrdersComplete,
            // Tasks
            Permissions.TasksView,
            Permissions.TasksCreate,
            Permissions.TasksUpdate,
            Permissions.TasksComplete,
            Permissions.TasksCompleteForOthers, // BR-12
            // Inventory
            Permissions.InventoryView,
            Permissions.InventoryCreate,
            Permissions.InventoryUpdate,
            // Catalog
            Permissions.CatalogView,
            // Quotations
            Permissions.QuotationsView,
            Permissions.QuotationsCreate,
            Permissions.QuotationsUpdate,
            // Deliveries
            Permissions.DeliveriesView,
            Permissions.DeliveriesComplete,
            Permissions.DeliveriesChecklist,
        },

        // ========== Designer ==========
        [UserRole.Designer] = new List<string>
        {
            // Orders
            Permissions.OrdersView,
            // Job Orders
            Permissions.JobOrdersView,
            // Tasks
            Permissions.TasksView,
            Permissions.TasksCreate,
            Permissions.TasksUpdate,
            Permissions.TasksComplete,
            // Designs
            Permissions.DesignsView,
            Permissions.DesignsCreate,
            Permissions.DesignsUpdate,
            // Catalog
            Permissions.CatalogView,
        },

        // ========== Driver ==========
        [UserRole.Driver] = new List<string>
        {
            // Orders
            Permissions.OrdersView,
            // Job Orders
            Permissions.JobOrdersView,
            // Tasks
            Permissions.TasksView,
            Permissions.TasksComplete,
            // Deliveries
            Permissions.DeliveriesView,
            Permissions.DeliveriesComplete,
            Permissions.DeliveriesChecklist,
            // Catalog
            Permissions.CatalogView,
        },

        // ========== Admin ==========
        [UserRole.Admin] = Permissions.GetAllPermissions(),
    };

    public static List<string> GetPermissionsForRole(UserRole role)
    {
        return Map.TryGetValue(role, out var permissions) 
            ? permissions 
            : new List<string>();
    }

    public static bool HasPermission(UserRole role, string permission)
    {
        if (role == UserRole.Admin)
            return true;
        
        return Map.TryGetValue(role, out var permissions) && 
               permissions.Contains(permission);
    }
}