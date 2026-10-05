namespace TIAdmin.Infrastructure.Extensions;

using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Infrastructure.Identity;
using TIAdmin.Infrastructure.Persistence;
using TIAdmin.Infrastructure.Persistence.Interceptors;
using TIAdmin.Infrastructure.Persistence.Seeding;
using TIAdmin.Infrastructure.Persistence.Repositories;
using TIAdmin.Infrastructure.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "No se encontro la cadena de conexion 'DefaultConnection'. "
                + "Defina ConnectionStrings:DefaultConnection o la variable de entorno ConnectionStrings__DefaultConnection.");

        services.AddSingleton<IClock, SystemClock>();
        services.AddMemoryCache();

        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<AuditTrailInterceptor>();

        services.AddDbContext<TIAdminDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(TIAdminDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
                sql.CommandTimeout(60);
            });

            options.AddInterceptors(
                provider.GetRequiredService<AuditSaveChangesInterceptor>(),
                provider.GetRequiredService<AuditTrailInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IAssetAssignmentRepository, AssetAssignmentRepository>();
        services.AddScoped<IAssetMovementRepository, AssetMovementRepository>();
        services.AddScoped<IAssetTypeRepository, AssetTypeRepository>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<ISystemSettings, SystemSettings>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IReportingQueries, Reporting.ReportingQueries>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(provider => provider.GetRequiredService<EmailQueue>());
        if (configuration.GetValue<bool>("Email:Enabled"))
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }

        services.AddHostedService<EmailDispatchWorker>();
        services.AddHostedService<AlertScanWorker>();

        services.AddSingleton<TabularFiles>();
        services.AddSingleton<ITabularFileWriter>(provider => provider.GetRequiredService<TabularFiles>());
        services.AddSingleton<ITabularFileReader>(provider => provider.GetRequiredService<TabularFiles>());
        services.AddSingleton<ExportQueue>();
        services.AddSingleton<IExportQueue>(provider => provider.GetRequiredService<ExportQueue>());
        services.AddHostedService<ExportWorker>();

        AddSecretProtection(services, configuration);

        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    /// <summary>
    /// Q-09 / ADR-021: cifrado de secretos de negocio con ASP.NET Core Data Protection.
    /// <list type="bullet">
    /// <item><c>DataProtection:KeysPath</c>: carpeta persistente (compartida entre instancias) para el anillo de claves.</item>
    /// <item><c>DataProtection:CertificateThumbprint</c>: certificado (almacen My de CurrentUser o LocalMachine) que
    /// cifra el anillo en reposo; sin el, una copia de la carpeta basta para descifrar.</item>
    /// </list>
    /// Sin configuracion (desarrollo) se usa el perfil del usuario local. Si se pierde el anillo, las claves de
    /// licencia cifradas son irrecuperables: respaldarlo junto con el certificado, aparte de la base de datos.
    /// </summary>
    private static void AddSecretProtection(IServiceCollection services, IConfiguration configuration)
    {
        var dataProtection = services.AddDataProtection().SetApplicationName("TIAdmin");

        if (configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        if (configuration["DataProtection:CertificateThumbprint"] is { Length: > 0 } thumbprint)
        {
            dataProtection.ProtectKeysWithCertificate(FindCertificate(thumbprint));
        }

        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
    }

    /// <summary>Falla al arrancar si el certificado configurado no existe: mejor que cifrar sin proteccion.</summary>
    private static X509Certificate2 FindCertificate(string thumbprint)
    {
        var normalized = thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly);
            var matches = store.Certificates.Find(X509FindType.FindByThumbprint, normalized, validOnly: false);
            if (matches.Count > 0)
            {
                return matches[0];
            }
        }

        throw new InvalidOperationException(
            $"No se encontro el certificado de Data Protection con huella {normalized} en CurrentUser/My ni LocalMachine/My.");
    }

    public static IServiceCollection AddIdentity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<TIAdminDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IRoleManagementService, RoleManagementService>();

        return services;
    }

    public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName));

        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName));

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName));

        services.AddOptions<CacheOptions>()
            .Bind(configuration.GetSection(CacheOptions.SectionName));

        services.AddOptions<SeedOptions>()
            .Bind(configuration.GetSection(SeedOptions.SectionName));

        return services;
    }
}
