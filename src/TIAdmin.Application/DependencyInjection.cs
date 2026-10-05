namespace TIAdmin.Application;

using Microsoft.Extensions.DependencyInjection;
using TIAdmin.Application.Common;
using TIAdmin.Application.Assets;
using TIAdmin.Application.HelpDesk;
using TIAdmin.Application.Licensing;
using TIAdmin.Application.Operations;
using TIAdmin.Application.Vendors;

public static class DependencyInjection
{
    /// <summary>Servicios de casos de uso de Application.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

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

        return services;
    }
}
