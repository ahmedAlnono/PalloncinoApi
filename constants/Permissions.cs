namespace Palloncino.Core.Constants;

public static class Permissions
{
    // ========== Order Module ==========
    public const string Orders = "orders";
    public const string OrdersView = "orders.view";
    public const string OrdersCreate = "orders.create";
    public const string OrdersUpdate = "orders.update";
    public const string OrdersDelete = "orders.delete";
    public const string OrdersApprove = "orders.approve";
    public const string OrdersReject = "orders.reject";

    // ========== Job Order Module ==========
    public const string JobOrders = "joborders";
    public const string JobOrdersView = "joborders.view";
    public const string JobOrdersCreate = "joborders.create";
    public const string JobOrdersUpdate = "joborders.update";
    public const string JobOrdersDelete = "joborders.delete";
    public const string JobOrdersAssign = "joborders.assign";
    public const string JobOrdersComplete = "joborders.complete";
    public const string JobOrdersSkipReturn = "joborders.skipreturn";

    // ========== Task Module ==========
    public const string Tasks = "tasks";
    public const string TasksView = "tasks.view";
    public const string TasksCreate = "tasks.create";
    public const string TasksUpdate = "tasks.update";
    public const string TasksDelete = "tasks.delete";
    public const string TasksComplete = "tasks.complete";
    public const string TasksCompleteForOthers = "tasks.completeforothers"; // BR-12

    // ========== Inventory Module ==========
    public const string Inventory = "inventory";
    public const string InventoryView = "inventory.view";
    public const string InventoryCreate = "inventory.create";
    public const string InventoryUpdate = "inventory.update";
    public const string InventoryDelete = "inventory.delete";
    public const string InventoryAdjust = "inventory.adjust";
    public const string InventoryTransfer = "inventory.transfer";

    // ========== Catalog Module ==========
    public const string Catalog = "catalog";
    public const string CatalogView = "catalog.view";
    public const string CatalogCreate = "catalog.create";
    public const string CatalogUpdate = "catalog.update";
    public const string CatalogDelete = "catalog.delete";

    // ========== Quotation Module ==========
    public const string Quotations = "quotations";
    public const string QuotationsView = "quotations.view";
    public const string QuotationsCreate = "quotations.create";
    public const string QuotationsUpdate = "quotations.update";
    public const string QuotationsDelete = "quotations.delete";
    public const string QuotationsApprove = "quotations.approve";

    // ========== User Management ==========
    public const string Users = "users";
    public const string UsersView = "users.view";
    public const string UsersCreate = "users.create";
    public const string UsersUpdate = "users.update";
    public const string UsersDelete = "users.delete";
    public const string UsersAssignRole = "users.assignrole";
    public const string UsersAssignBranch = "users.assignbranch";

    // ========== Reports ==========
    public const string Reports = "reports";
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";
    public const string ReportsFinancial = "reports.financial";
    public const string ReportsPerformance = "reports.performance";

    // ========== System ==========
    public const string System = "system";
    public const string SystemSettings = "system.settings";
    public const string SystemAuditLog = "system.auditlog";
    public const string SystemBroadcast = "system.broadcast";

    // ========== Branch Management ==========
    public const string Branches = "branches";
    public const string BranchesView = "branches.view";
    public const string BranchesCreate = "branches.create";
    public const string BranchesUpdate = "branches.update";
    public const string BranchesDelete = "branches.delete";

    // ========== Design Module ==========
    public const string Designs = "designs";
    public const string DesignsView = "designs.view";
    public const string DesignsCreate = "designs.create";
    public const string DesignsUpdate = "designs.update";
    public const string DesignsDelete = "designs.delete";

    // ========== Delivery Module ==========
    public const string Deliveries = "deliveries";
    public const string DeliveriesView = "deliveries.view";
    public const string DeliveriesComplete = "deliveries.complete";
    public const string DeliveriesChecklist = "deliveries.checklist";

    // ========== Helper Methods ==========
    public static List<string> GetAllPermissions()
    {
        return typeof(Permissions)
            .GetFields()
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => f.GetValue(null)?.ToString())
            .Where(v => v != null)
            .ToList()!;
    }

    public static bool IsHierarchical(string permission)
    {
        // Check if permission has hierarchy (e.g., "orders.view" has parent "orders")
        var parts = permission.Split('.');
        return parts.Length > 1;
    }

    public static string GetParentPermission(string permission)
    {
        var parts = permission.Split('.');
        if (parts.Length <= 1)
            return permission;
        
        return parts[0];
    }
}