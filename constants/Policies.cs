namespace Palloncino.Core.Constants;

public static class Policies
{
    // ========== Order Policies ==========
    public const string OrderView = "OrderView";
    public const string OrderCreate = "OrderCreate";
    public const string OrderUpdate = "OrderUpdate";
    public const string OrderDelete = "OrderDelete";
    public const string OrderApprove = "OrderApprove";
    public const string OrderReject = "OrderReject";

    // ========== Job Order Policies ==========
    public const string JobOrderView = "JobOrderView";
    public const string JobOrderCreate = "JobOrderCreate";
    public const string JobOrderUpdate = "JobOrderUpdate";
    public const string JobOrderDelete = "JobOrderDelete";
    public const string JobOrderAssign = "JobOrderAssign";
    public const string JobOrderComplete = "JobOrderComplete";
    public const string JobOrderSkipReturn = "JobOrderSkipReturn";

    // ========== Task Policies ==========
    public const string TaskView = "TaskView";
    public const string TaskCreate = "TaskCreate";
    public const string TaskUpdate = "TaskUpdate";
    public const string TaskDelete = "TaskDelete";
    public const string TaskComplete = "TaskComplete";
    public const string TaskCompleteForOthers = "TaskCompleteForOthers";

    // ========== Inventory Policies ==========
    public const string InventoryView = "InventoryView";
    public const string InventoryCreate = "InventoryCreate";
    public const string InventoryUpdate = "InventoryUpdate";
    public const string InventoryDelete = "InventoryDelete";
    public const string InventoryAdjust = "InventoryAdjust";
    public const string InventoryTransfer = "InventoryTransfer";

    // ========== Catalog Policies ==========
    public const string CatalogView = "CatalogView";
    public const string CatalogCreate = "CatalogCreate";
    public const string CatalogUpdate = "CatalogUpdate";
    public const string CatalogDelete = "CatalogDelete";

    // ========== Quotation Policies ==========
    public const string QuotationView = "QuotationView";
    public const string QuotationCreate = "QuotationCreate";
    public const string QuotationUpdate = "QuotationUpdate";
    public const string QuotationDelete = "QuotationDelete";
    public const string QuotationApprove = "QuotationApprove";

    // ========== User Management Policies ==========
    public const string UserView = "UserView";
    public const string UserCreate = "UserCreate";
    public const string UserUpdate = "UserUpdate";
    public const string UserDelete = "UserDelete";
    public const string UserAssignRole = "UserAssignRole";
    public const string UserAssignBranch = "UserAssignBranch";

    // ========== Report Policies ==========
    public const string ReportView = "ReportView";
    public const string ReportExport = "ReportExport";
    public const string ReportFinancial = "ReportFinancial";
    public const string ReportPerformance = "ReportPerformance";

    // ========== System Policies ==========
    public const string SystemSettings = "SystemSettings";
    public const string SystemAuditLog = "SystemAuditLog";
    public const string SystemBroadcast = "SystemBroadcast";

    // ========== Branch Policies ==========
    public const string BranchView = "BranchView";
    public const string BranchCreate = "BranchCreate";
    public const string BranchUpdate = "BranchUpdate";
    public const string BranchDelete = "BranchDelete";

    // ========== Design Policies ==========
    public const string DesignView = "DesignView";
    public const string DesignCreate = "DesignCreate";
    public const string DesignUpdate = "DesignUpdate";
    public const string DesignDelete = "DesignDelete";

    // ========== Delivery Policies ==========
    public const string DeliveryView = "DeliveryView";
    public const string DeliveryComplete = "DeliveryComplete";
    public const string DeliveryChecklist = "DeliveryChecklist";

    // ========== Helper Methods ==========
    public static string GetPermissionFromPolicy(string policy)
    {
        return policy switch
        {
            OrderView => Permissions.OrdersView,
            OrderCreate => Permissions.OrdersCreate,
            OrderUpdate => Permissions.OrdersUpdate,
            OrderDelete => Permissions.OrdersDelete,
            OrderApprove => Permissions.OrdersApprove,
            OrderReject => Permissions.OrdersReject,
            JobOrderView => Permissions.JobOrdersView,
            JobOrderCreate => Permissions.JobOrdersCreate,
            JobOrderUpdate => Permissions.JobOrdersUpdate,
            JobOrderDelete => Permissions.JobOrdersDelete,
            JobOrderAssign => Permissions.JobOrdersAssign,
            JobOrderComplete => Permissions.JobOrdersComplete,
            JobOrderSkipReturn => Permissions.JobOrdersSkipReturn,
            TaskView => Permissions.TasksView,
            TaskCreate => Permissions.TasksCreate,
            TaskUpdate => Permissions.TasksUpdate,
            TaskDelete => Permissions.TasksDelete,
            TaskComplete => Permissions.TasksComplete,
            TaskCompleteForOthers => Permissions.TasksCompleteForOthers,
            InventoryView => Permissions.InventoryView,
            InventoryCreate => Permissions.InventoryCreate,
            InventoryUpdate => Permissions.InventoryUpdate,
            InventoryDelete => Permissions.InventoryDelete,
            InventoryAdjust => Permissions.InventoryAdjust,
            InventoryTransfer => Permissions.InventoryTransfer,
            CatalogView => Permissions.CatalogView,
            CatalogCreate => Permissions.CatalogCreate,
            CatalogUpdate => Permissions.CatalogUpdate,
            CatalogDelete => Permissions.CatalogDelete,
            QuotationView => Permissions.QuotationsView,
            QuotationCreate => Permissions.QuotationsCreate,
            QuotationUpdate => Permissions.QuotationsUpdate,
            QuotationDelete => Permissions.QuotationsDelete,
            QuotationApprove => Permissions.QuotationsApprove,
            UserView => Permissions.UsersView,
            UserCreate => Permissions.UsersCreate,
            UserUpdate => Permissions.UsersUpdate,
            UserDelete => Permissions.UsersDelete,
            UserAssignRole => Permissions.UsersAssignRole,
            UserAssignBranch => Permissions.UsersAssignBranch,
            ReportView => Permissions.ReportsView,
            ReportExport => Permissions.ReportsExport,
            ReportFinancial => Permissions.ReportsFinancial,
            ReportPerformance => Permissions.ReportsPerformance,
            SystemSettings => Permissions.SystemSettings,
            SystemAuditLog => Permissions.SystemAuditLog,
            SystemBroadcast => Permissions.SystemBroadcast,
            BranchView => Permissions.BranchesView,
            BranchCreate => Permissions.BranchesCreate,
            BranchUpdate => Permissions.BranchesUpdate,
            BranchDelete => Permissions.BranchesDelete,
            DesignView => Permissions.DesignsView,
            DesignCreate => Permissions.DesignsCreate,
            DesignUpdate => Permissions.DesignsUpdate,
            DesignDelete => Permissions.DesignsDelete,
            DeliveryView => Permissions.DeliveriesView,
            DeliveryComplete => Permissions.DeliveriesComplete,
            DeliveryChecklist => Permissions.DeliveriesChecklist,
            _ => policy
        };
    }
}