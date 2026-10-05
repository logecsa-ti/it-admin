namespace TIAdmin.Application.Common.Constants;

/// <summary>
/// Catalogo de permisos del sistema (SPECS.md seccion 16).
/// Los permisos son independientes de los roles: permiten crear roles nuevos
/// sin modificar codigo.
/// </summary>
public static class Permissions
{
    // Assets
    public const string AssetsView = "ASSETS.VIEW";
    public const string AssetsCreate = "ASSETS.CREATE";
    public const string AssetsUpdate = "ASSETS.UPDATE";
    public const string AssetsDelete = "ASSETS.DELETE";
    public const string AssetsAssign = "ASSETS.ASSIGN";
    public const string AssetsUnassign = "ASSETS.UNASSIGN";

    // Asset types
    public const string AssetTypesManage = "ASSET_TYPES.MANAGE";

    // Assignments
    public const string AssignmentsView = "ASSIGNMENTS.VIEW";
    public const string AssignmentsManage = "ASSIGNMENTS.MANAGE";

    // Software
    public const string SoftwareView = "SOFTWARE.VIEW";
    public const string SoftwareManage = "SOFTWARE.MANAGE";

    // Licenses
    public const string LicensesView = "LICENSES.VIEW";
    public const string LicensesManage = "LICENSES.MANAGE";

    // Tickets
    public const string TicketsView = "TICKETS.VIEW";
    public const string TicketsCreate = "TICKETS.CREATE";
    public const string TicketsUpdate = "TICKETS.UPDATE";
    public const string TicketsAssign = "TICKETS.ASSIGN";
    public const string TicketsResolve = "TICKETS.RESOLVE";
    public const string TicketsClose = "TICKETS.CLOSE";

    // Service requests
    public const string RequestsView = "REQUESTS.VIEW";
    public const string RequestsCreate = "REQUESTS.CREATE";
    public const string RequestsApprove = "REQUESTS.APPROVE";
    public const string RequestsManage = "REQUESTS.MANAGE";

    // Maintenance
    public const string MaintenanceView = "MAINTENANCE.VIEW";
    public const string MaintenanceManage = "MAINTENANCE.MANAGE";

    // Vendors
    public const string VendorsView = "VENDORS.VIEW";
    public const string VendorsManage = "VENDORS.MANAGE";

    // Contracts
    public const string ContractsView = "CONTRACTS.VIEW";
    public const string ContractsManage = "CONTRACTS.MANAGE";

    // Purchases
    public const string PurchasesView = "PURCHASES.VIEW";
    public const string PurchasesCreate = "PURCHASES.CREATE";
    public const string PurchasesApprove = "PURCHASES.APPROVE";
    public const string PurchasesManage = "PURCHASES.MANAGE";

    // Changes
    public const string ChangesView = "CHANGES.VIEW";
    public const string ChangesCreate = "CHANGES.CREATE";
    public const string ChangesReview = "CHANGES.REVIEW";
    public const string ChangesManage = "CHANGES.MANAGE";

    // Documents
    public const string DocumentsView = "DOCUMENTS.VIEW";
    public const string DocumentsManage = "DOCUMENTS.MANAGE";

    // Users
    public const string UsersView = "USERS.VIEW";
    public const string UsersCreate = "USERS.CREATE";
    public const string UsersUpdate = "USERS.UPDATE";
    public const string UsersDisable = "USERS.DISABLE";

    // Roles & permissions
    public const string RolesView = "ROLES.VIEW";
    public const string RolesManage = "ROLES.MANAGE";

    // Organization
    public const string OrganizationView = "ORGANIZATION.VIEW";
    public const string OrganizationManage = "ORGANIZATION.MANAGE";

    // Reports
    public const string ReportsView = "REPORTS.VIEW";
    public const string ReportsExport = "REPORTS.EXPORT";

    // Audit
    public const string AuditView = "AUDIT.VIEW";

    // Configuration
    public const string ConfigurationView = "CONFIGURATION.VIEW";
    public const string ConfigurationManage = "CONFIGURATION.MANAGE";

    // Dashboard
    public const string DashboardView = "DASHBOARD.VIEW";

    // Notifications
    public const string NotificationsView = "NOTIFICATIONS.VIEW";

    /// <summary>
    /// Lista completa de permisos con su modulo y accion, usada para el seed.
    /// </summary>
    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new("Assets", "View", "Consultar activos"),
        new("Assets", "Create", "Crear activos"),
        new("Assets", "Update", "Actualizar activos"),
        new("Assets", "Delete", "Desactivar activos"),
        new("Assets", "Assign", "Asignar activos a usuarios"),
        new("Assets", "Unassign", "Desasignar activos"),
        new("AssetTypes", "Manage", "Administrar tipos de activo"),
        new("Assignments", "View", "Consultar asignaciones"),
        new("Assignments", "Manage", "Administrar asignaciones"),
        new("Software", "View", "Consultar catalogo de software"),
        new("Software", "Manage", "Administrar catalogo de software"),
        new("Licenses", "View", "Consultar licencias"),
        new("Licenses", "Manage", "Administrar licencias"),
        new("Tickets", "View", "Consultar tickets"),
        new("Tickets", "Create", "Crear tickets"),
        new("Tickets", "Update", "Actualizar tickets"),
        new("Tickets", "Assign", "Asignar tickets"),
        new("Tickets", "Resolve", "Resolver tickets"),
        new("Tickets", "Close", "Cerrar tickets"),
        new("Requests", "View", "Consultar solicitudes"),
        new("Requests", "Create", "Crear solicitudes"),
        new("Requests", "Approve", "Aprobar solicitudes"),
        new("Requests", "Manage", "Administrar solicitudes"),
        new("Maintenance", "View", "Consultar mantenimientos"),
        new("Maintenance", "Manage", "Administrar mantenimientos"),
        new("Vendors", "View", "Consultar proveedores"),
        new("Vendors", "Manage", "Administrar proveedores"),
        new("Contracts", "View", "Consultar contratos"),
        new("Contracts", "Manage", "Administrar contratos"),
        new("Purchases", "View", "Consultar compras"),
        new("Purchases", "Create", "Crear solicitudes de compra"),
        new("Purchases", "Approve", "Aprobar compras"),
        new("Purchases", "Manage", "Administrar compras"),
        new("Changes", "View", "Consultar cambios"),
        new("Changes", "Create", "Solicitar cambios"),
        new("Changes", "Review", "Revisar y aprobar cambios"),
        new("Changes", "Manage", "Administrar cambios"),
        new("Documents", "View", "Consultar documentos"),
        new("Documents", "Manage", "Cargar y eliminar documentos"),
        new("Users", "View", "Consultar usuarios"),
        new("Users", "Create", "Crear usuarios"),
        new("Users", "Update", "Actualizar usuarios"),
        new("Users", "Disable", "Desactivar usuarios"),
        new("Roles", "View", "Consultar roles y permisos"),
        new("Roles", "Manage", "Administrar roles y permisos"),
        new("Organization", "View", "Consultar departamentos y ubicaciones"),
        new("Organization", "Manage", "Administrar departamentos y ubicaciones"),
        new("Reports", "View", "Consultar reportes"),
        new("Reports", "Export", "Exportar reportes"),
        new("Audit", "View", "Consultar bitacora de auditoria"),
        new("Configuration", "View", "Consultar configuracion"),
        new("Configuration", "Manage", "Administrar configuracion"),
        new("Dashboard", "View", "Consultar el dashboard"),
        new("Notifications", "View", "Consultar notificaciones")
    ];

    public static string[] SuperAdminPermissions => [.. All.Select(p => p.Code)];

    public static IReadOnlyList<string> ForRole(string role) => role switch
    {
        SystemRoles.SuperAdmin => SuperAdminPermissions,

        SystemRoles.TiAdmin =>
        [
            .. All.Where(p => !p.Code.StartsWith("ROLES.", StringComparison.Ordinal)).Select(p => p.Code),
            RolesView
        ],

        SystemRoles.TiAssetManager =>
        [
            AssetsView, AssetsCreate, AssetsUpdate, AssetsAssign, AssetsUnassign, AssetTypesManage,
            AssignmentsView, AssignmentsManage, SoftwareView, SoftwareManage, LicensesView, LicensesManage,
            VendorsView, VendorsManage, ContractsView, DocumentsView, DocumentsManage,
            MaintenanceView, MaintenanceManage, UsersView, OrganizationView, DashboardView,
            NotificationsView, ReportsView
        ],

        SystemRoles.TiSupport =>
        [
            TicketsView, TicketsCreate, TicketsUpdate, TicketsAssign, TicketsResolve, TicketsClose,
            RequestsView, RequestsCreate,
            MaintenanceView, MaintenanceManage,
            AssetsView, AssignmentsView, AssetsUpdate,
            VendorsView, SoftwareView, LicensesView,
            UsersView, OrganizationView, DashboardView, NotificationsView,
            ChangesView, ChangesCreate, DocumentsView
        ],

        SystemRoles.TiManager =>
        [
            AssetsView, AssignmentsView, SoftwareView, LicensesView, TicketsView, RequestsView,
            RequestsApprove, MaintenanceView, VendorsView, ContractsView, PurchasesView, PurchasesApprove,
            ChangesView, ChangesReview, ChangesManage, DocumentsView, UsersView, OrganizationView,
            ReportsView, ReportsExport, AuditView, DashboardView, NotificationsView
        ],

        SystemRoles.Auditor =>
        [
            AssetsView, SoftwareView, LicensesView, TicketsView, RequestsView, MaintenanceView,
            VendorsView, ContractsView, PurchasesView, ChangesView, UsersView, OrganizationView,
            ReportsView, ReportsExport, AuditView, DashboardView
        ],

        // Sin ASSETS.VIEW: un usuario final solo consulta sus propios activos (GET /assets/mine), no el inventario (Q-10).
        SystemRoles.User =>
        [
            TicketsView, TicketsCreate, RequestsView, RequestsCreate,
            DashboardView, NotificationsView
        ],

        _ => []
    };
}

public sealed record PermissionDefinition(string Module, string Action, string Description)
{
    /// <summary>
    /// Codigo normalizado del permiso, derivado de Modulo + Accion para evitar divergencias.
    /// Ejemplo: Module="AssetTypes", Action="Manage" -> "ASSET_TYPES.MANAGE".
    /// </summary>
    public string Code => $"{ToSnakeCase(Module)}.{Action.ToUpperInvariant()}";

    private static string ToSnakeCase(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 4);

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (char.IsUpper(c) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }
}
