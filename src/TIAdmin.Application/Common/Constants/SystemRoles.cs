namespace TIAdmin.Application.Common.Constants;

public static class SystemRoles
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string TiAdmin = "TI_ADMIN";
    public const string TiSupport = "TI_SUPPORT";
    public const string TiAssetManager = "TI_ASSET_MANAGER";
    public const string TiManager = "TI_MANAGER";
    public const string Auditor = "AUDITOR";
    public const string User = "USER";

    public static readonly IReadOnlyList<string> All =
    [
        SuperAdmin, TiAdmin, TiSupport, TiAssetManager, TiManager, Auditor, User
    ];

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [SuperAdmin] = "Acceso total al sistema, incluida la configuracion de seguridad",
        [TiAdmin] = "Administracion completa del inventario y soporte de TI",
        [TiSupport] = "Gestion de tickets, mantenimientos y atencion de solicitudes",
        [TiAssetManager] = "Gestion del ciclo de vida de activos y asignaciones",
        [TiManager] = "Supervision, aprobaciones, reportes gerenciales y costos",
        [Auditor] = "Acceso de solo lectura a auditoria y reportes",
        [User] = "Solicitud de tickets y consulta de sus propios activos"
    };
}
