using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using TIAdmin.Api.Filters;
using TIAdmin.Api.Middleware;
using TIAdmin.Api.Services;
using TIAdmin.Application;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Infrastructure.Extensions;
using TIAdmin.Infrastructure.Identity;
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
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidatableRequestAttribute>();
    })
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
        Description = "Sistema Administrativo de Tecnologias de Informacion (SPECS.md v1.0)"
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
builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddIdentity();

// ---------- Autenticacion JWT (SPECS.md seccion 15.1) ----------
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "Falta la seccion 'Jwt' en la configuracion. Ver docs/DEV_MEMORY.md.");

// AddIdentity registra la cookie de Identity como DefaultAuthenticate/DefaultChallenge;
// esos valores tienen prioridad sobre DefaultScheme, asi que se fijan todos a JWT.
// Sin esto el token nunca se valida y los 401 redirigen a /Account/Login.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                // Un 401 nunca debe devolver el HTML del middleware de challenge.
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json; charset=utf-8";
                return context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    data = (object?)null,
                    message = "No autenticado.",
                    errors = new[] { new { code = "UNAUTHORIZED", message = "No autenticado." } }
                });
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";
                return context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    data = (object?)null,
                    message = "No tiene permisos para esta operacion.",
                    errors = new[] { new { code = "FORBIDDEN", message = "No tiene permisos para esta operacion." } }
                });
            }
        };
    });

// ---------- Autorizacion por permiso (SPECS.md seccion 16) ----------
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

var allPermissions = TIAdmin.Application.Common.Constants.Permissions.All
    .Select(p => p.Code)
    .ToArray();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    foreach (var permission in allPermissions)
    {
        options.AddPolicy(permission, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement(permission));
        });
    }
});

// ---------- Puertos de aplicacion ----------
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuditContext, AuditContext>();

// ---------- Rate limiting (SPECS.md seccion 17) ----------
var rateLimitOptions = builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
    ?? new RateLimitOptions();

if (rateLimitOptions.Enabled)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Politica global: trafico general de la API.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitOptions.GeneralPermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitOptions.GeneralWindowSeconds),
                    QueueLimit = 0
                }));

        // Politica estricta para endpoints de autenticacion (mitiga fuerza bruta).
        options.AddPolicy("login", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitOptions.LoginPermitLimit,
                    Window = TimeSpan.FromMinutes(rateLimitOptions.LoginWindowMinutes),
                    QueueLimit = 0
                }));
    });
}

// ---------- Health Checks (SPECS.md seccion 42) ----------
builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty,
        name: "database",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API operativa"),
        tags: ["live"]);

var app = builder.Build();

// ---------- Pipeline: el orden importa ----------
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

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

// CORS restringido a los origenes configurados (SPECS.md seccion 17).
// Sin origenes configurados se aplica una politica vacia: fallo cerrado.
var allowedOrigins = builder.Configuration.GetSection(CorsOptions.SectionName)
    .Get<CorsOptions>()?.AllowedOrigins ?? [];

app.UseCors(policy =>
{
    if (allowedOrigins.Length == 0)
    {
        return;
    }

    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)
        .SetPreflightMaxAge(TimeSpan.FromHours(1));
});

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} respondio {StatusCode} en {Elapsed:0.0000} ms";
});

if (rateLimitOptions.Enabled)
{
    app.UseRateLimiter();
}

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
