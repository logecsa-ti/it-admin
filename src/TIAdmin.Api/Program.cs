using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using TIAdmin.Infrastructure.Extensions;
using TIAdmin.Infrastructure.Persistence.Seeding;
using TIAdmin.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging estructurado (SPECS.md seccion 41) ----------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "TIAdmin.Api")
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .WriteTo.Console());

// ---------- Servicios base ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TI Admin API",
        Version = "v1",
        Description = "Sistema Administrativo de Tecnologias de Informacion (SPECS.md v1.0)",
        Contact = new OpenApiContact { Name = "Equipo TI" }
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Introduzca el token JWT: {your-token}"
    };

    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer")] = []
    });
});

builder.Services.AddApplicationOptions(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddIdentity();

// ICurrentUserService depende de IHttpContextAccessor
builder.Services.AddScoped<TIAdmin.Application.Common.Interfaces.ICurrentUserService, CurrentUserService>();

// ---------- Health Checks (SPECS.md seccion 42) ----------
builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty,
        name: "database",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API operativa"), tags: ["live"]);

var app = builder.Build();

// ---------- Pipeline ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TI Admin API v1");
        options.DocumentTitle = "TI Admin API";
    });
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} respondió {StatusCode} en {Elapsed:0.0000} ms";
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ---------- Health endpoints ----------
app.MapHealthChecks("/health/live", new()
{
    Predicate = check => check.Tags.Contains("live")
}).AllowAnonymous();

app.MapHealthChecks("/health/ready").AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();

// ---------- Seed ----------
if (app.Configuration.GetValue<bool>("Seed:Enabled"))
{
    await SeedDatabaseAsync(app);
}

app.Run();
return;

static async Task SeedDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

/// <summary>
/// Punto de entrada expuesto para pruebas funcionales con WebApplicationFactory.
/// </summary>
public partial class Program
{
    protected Program()
    {
    }
}
