using System.ComponentModel.DataAnnotations;

namespace TIAdmin.Application.Common.Models;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required, MinLength(32)]
    public string SecretKey { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 30;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 7;

    [Range(1, 60)]
    public int ClockSkewSeconds { get; set; } = 30;
}

public sealed class AppOptions
{
    public const string SectionName = "App";

    public string Name { get; set; } = "TI Admin";

    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Zona horaria de presentación hacia el usuario final (SPECS.md sección 38).
    /// El almacenamiento es siempre en UTC.
    /// </summary>
    public string TimeZone { get; set; } = "America/Managua";

    public int DefaultPageSize { get; set; } = 25;

    public int MaxPageSize { get; set; } = 200;
}

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public int GeneralPermitLimit { get; set; } = 300;

    public int GeneralWindowSeconds { get; set; } = 60;

    public int LoginPermitLimit { get; set; } = 5;

    public int LoginWindowMinutes { get; set; } = 1;
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Proveedor de almacenamiento: "local" (desarrollo) o "blob" (produccion).
    /// </summary>
    public string Provider { get; set; } = "local";

    public string RootPath { get; set; } = "./storage";

    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } =
        [".pdf", ".png", ".jpg", ".jpeg", ".gif", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt", ".zip"];
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; } = false;

    public string From { get; set; } = "no-reply@tiadmin.local";

    public string FromName { get; set; } = "TI Admin";

    public string SmtpHost { get; set; } = "localhost";

    public int SmtpPort { get; set; } = 25;

    public bool UseSsl { get; set; } = false;

    public string? UserName { get; set; }

    public string? Password { get; set; }
}

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Proveedor de cache: "memory" o "redis".
    /// </summary>
    public string Provider { get; set; } = "memory";

    public string? RedisConnectionString { get; set; }

    public int DefaultExpirationMinutes { get; set; } = 30;
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>
    /// Habilita el seed de datos iniciales (roles, permisos, usuario admin).
    /// Debe ser false en produccion.
    /// </summary>
    public bool Enabled { get; set; } = false;

    public string AdminUserName { get; set; } = "admin";

    public string AdminEmail { get; set; } = "admin@tiadmin.local";

    /// <summary>
    /// Contrasena inicial del administrador. Se inyecta por variable de entorno.
    /// </summary>
    public string? AdminPassword { get; set; }
}
