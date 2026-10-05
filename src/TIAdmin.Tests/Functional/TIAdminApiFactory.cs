namespace TIAdmin.Tests.Functional;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Infrastructure.Identity;
using TIAdmin.Infrastructure.Persistence;
using TIAdmin.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Levanta la API completa (pipeline real: middleware, JWT, politicas, filtros) sobre
/// EF Core InMemory. El seeder crea permisos, roles y el admin al arrancar.
/// </summary>
public class TIAdminApiFactory : WebApplicationFactory<Program>
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "Admin123!Test";
    public const string AssetManagerUserName = "asset.manager";
    public const string AssetManagerPassword = "Manager123!Test";
    public const string AllowedOrigin = "http://localhost:4200";

    private readonly string databaseName = $"TIAdminTests-{Guid.NewGuid()}";

    /// <summary>Carpeta de archivos propia de esta factory (se elimina al liberarla).</summary>
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "tiadmin-tests", Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string> tokens = [];
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private bool usersSeeded;

    /// <summary>Limite del endpoint de login; alto por defecto para no interferir entre tests.</summary>
    protected virtual int LoginPermitLimit => 1000;

    /// <summary>Cadena de conexion de SQL Server; null usa EF InMemory (por defecto).</summary>
    protected virtual string? SqlServerConnectionString => null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Program.cs lee configuracion antes de Build(): UseSetting la aplica desde el inicio.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", SqlServerConnectionString ?? "Server=unused;Database=unused");
        builder.UseSetting("Jwt:Issuer", "https://tiadmin.tests");
        builder.UseSetting("Jwt:Audience", "tiadmin-tests");
        builder.UseSetting("Jwt:SecretKey", "TEST_ONLY_SECRET_KEY_0123456789ABCDEFGHIJ");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:AdminUserName", AdminUserName);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);
        builder.UseSetting("RateLimiting:Enabled", "true");
        builder.UseSetting("RateLimiting:LoginPermitLimit", LoginPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Jobs:Enabled", "false");
        builder.UseSetting("Storage:RootPath", StorageRoot);

        builder.ConfigureTestServices(services =>
        {
            if (SqlServerConnectionString is not null)
            {
                return; // registro real de Infrastructure (SQL Server, interceptores, reintentos)
            }

            // Sustituye SQL Server por InMemory conservando los interceptores reales.
            var sqlServerRegistrations = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<TIAdminDbContext>)
                    || d.ServiceType == typeof(IDbContextOptionsConfiguration<TIAdminDbContext>))
                .ToList();
            foreach (var descriptor in sqlServerRegistrations)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<TIAdminDbContext>((provider, options) => options
                .UseInMemoryDatabase(databaseName)
                .AddInterceptors(
                    provider.GetRequiredService<AuditSaveChangesInterceptor>(),
                    provider.GetRequiredService<AuditTrailInterceptor>()));
        });
    }

    public HttpClient CreateAnonymousClient() => CreateClient();

    public async Task<HttpClient> CreateAdminClientAsync() =>
        await CreateAuthenticatedClientAsync(AdminUserName, AdminPassword);

    public async Task<HttpClient> CreateAssetManagerClientAsync() =>
        await CreateAuthenticatedClientAsync(AssetManagerUserName, AssetManagerPassword);

    /// <summary>
    /// Inicia sesion via HTTP (una vez por usuario y factory) y devuelve un cliente con Bearer.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string userName, string password)
    {
        await tokenLock.WaitAsync();
        try
        {
            await EnsureTestUsersAsync();

            if (!tokens.TryGetValue(userName, out var token))
            {
                using var anonymous = CreateClient();
                var response = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
                token = body!.Data!.AccessToken;
                tokens[userName] = token;
            }

            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    /// <summary>
    /// Login sin cache: necesario cuando los roles/permisos del usuario cambiaron,
    /// porque viajan como claims en el token.
    /// </summary>
    public async Task<HttpClient> LoginFreshAsync(string userName, string password)
    {
        using var anonymous = CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }

    public async Task<T> WithDbContextAsync<T>(Func<TIAdminDbContext, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TIAdminDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            tokenLock.Dispose();
            if (Directory.Exists(StorageRoot))
            {
                Directory.Delete(StorageRoot, recursive: true);
            }
        }

        base.Dispose(disposing);
    }

    private async Task EnsureTestUsersAsync()
    {
        if (usersSeeded)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.FindByNameAsync(AssetManagerUserName) is null)
        {
            var user = new ApplicationUser(AssetManagerUserName, "asset.manager@tiadmin.tests")
            {
                FirstName = "Asset",
                LastName = "Manager"
            };
            var created = await userManager.CreateAsync(user, AssetManagerPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
            }

            await userManager.AddToRoleAsync(user, SystemRoles.TiAssetManager);
        }

        usersSeeded = true;
    }
}

/// <summary>Factory con limite de login bajo, para verificar el rate limiting.</summary>
public sealed class LowLoginLimitApiFactory : TIAdminApiFactory
{
    public const int Limit = 2;

    protected override int LoginPermitLimit => Limit;
}

/// <summary>Mismas convenciones JSON que la API (enums como texto), para leer respuestas tipadas.</summary>
public static class TestJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options =
        new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
}

public sealed record ApiEnvelope<T>(bool Success, T? Data, string? Message, IReadOnlyList<ApiErrorItem>? Errors, string? TraceId);

public sealed record ApiErrorItem(string Code, string Message);

public sealed record LoginData(string AccessToken, string RefreshToken);

public sealed record PagedData<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalItems, int TotalPages);
