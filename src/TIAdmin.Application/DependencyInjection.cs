namespace TIAdmin.Application;

using Microsoft.Extensions.DependencyInjection;
using TIAdmin.Application.Assets;

public static class DependencyInjection
{
    /// <summary>Servicios de casos de uso de Application.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAssetService, AssetService>();

        return services;
    }
}
