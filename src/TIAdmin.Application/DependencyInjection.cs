namespace TIAdmin.Application;

using Microsoft.Extensions.DependencyInjection;
using TIAdmin.Application.Administration;
using TIAdmin.Application.Common;
using TIAdmin.Application.Assets;
using TIAdmin.Application.HelpDesk;
using TIAdmin.Application.Licensing;
using TIAdmin.Application.Operations;
using TIAdmin.Application.Platform;
using TIAdmin.Application.Reporting;
using TIAdmin.Application.Vendors;

public static class DependencyInjection
{
    /// <summary>Servicios de casos de uso de Application.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<CatalogCache>();
        services.AddScoped<AssetMovementLog>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<ISoftwareService, SoftwareService>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IHelpDeskConfigService, HelpDeskConfigService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IChangeService, ChangeService>();
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAlertNotificationJob, AlertNotificationJob>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IAssetImportService, AssetImportService>();

        return services;
    }
}
