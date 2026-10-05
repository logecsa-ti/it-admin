namespace TIAdmin.Tests.Integration;

using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using TIAdmin.Infrastructure.Persistence;
using TIAdmin.Tests.Functional;

/// <summary>
/// La API completa sobre un SQL Server 2022 real en Docker (Testcontainers): valida lo que InMemory no puede,
/// como traduccion de consultas, migraciones, indices unicos/filtrados, rowversion y restricciones CHECK.
/// </summary>
public sealed class SqlServerApiFactory : TIAdminApiFactory, IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private string? connectionString;

    protected override string? SqlServerConnectionString =>
        connectionString ?? throw new InvalidOperationException("El contenedor de SQL Server no se ha iniciado.");

    public async Task InitializeAsync()
    {
        if (!DockerAvailability.IsAvailable)
        {
            return; // las pruebas se omiten con [DockerFact]
        }

        await container.StartAsync();
        connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = "TIAdminIntegration"
        }.ConnectionString;

        // La aplicacion no migra al arrancar (se hace en el despliegue): se aplican antes de levantar el host.
        var options = new DbContextOptionsBuilder<TIAdminDbContext>().UseSqlServer(connectionString).Options;
        await using var context = new TIAdminDbContext(options);
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await container.DisposeAsync();
    }
}
