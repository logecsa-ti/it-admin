namespace TIAdmin.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Factory used only by the EF Core tools (migrations, scripting).
/// It avoids booting the whole API host, which requires JWT secrets and a live database.
/// The connection string is read from appsettings or environment variables.
/// </summary>
public sealed class TIAdminDbContextFactory : IDesignTimeDbContextFactory<TIAdminDbContext>
{
    private const string FallbackConnectionString =
        "Server=localhost,1433;Database=TIAdminDb;User Id=sa;Password=LocalDev!Pass123;TrustServerCertificate=True;Encrypt=False";

    public TIAdminDbContext CreateDbContext(string[] args)
    {
        var basePath = FindProjectDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = FallbackConnectionString;
        }

        var optionsBuilder = new DbContextOptionsBuilder<TIAdminDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            sql => sql.MigrationsAssembly(typeof(TIAdminDbContext).Assembly.FullName));

        return new TIAdminDbContext(optionsBuilder.Options);
    }

    private static string FindProjectDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            if (directory.GetFiles("*.csproj").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
