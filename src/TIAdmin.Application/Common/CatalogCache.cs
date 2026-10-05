namespace TIAdmin.Application.Common;

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Cache en memoria de catalogos poco cambiantes (SPECS.md seccion 45): tipos de activo, categorias...
/// Cada catalogo puede tener variantes (p. ej. activos/inactivos) y se invalida completo al modificarse.
/// Con varias instancias cada una tiene su propia copia: la expiracion acota la desactualizacion.
/// </summary>
public sealed class CatalogCache(IMemoryCache cache)
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, CancellationTokenSource> tokens = new(StringComparer.Ordinal);

    public async Task<T> GetOrCreateAsync<T>(string catalog, string variant, Func<Task<T>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var key = $"catalog:{catalog}:{variant}";
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory();
        var token = tokens.GetOrAdd(catalog, _ => new CancellationTokenSource());
        cache.Set(key, value, new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(DefaultTtl)
            .AddExpirationToken(new CancellationChangeToken(token.Token)));
        return value;
    }

    /// <summary>Descarta todas las variantes del catalogo.</summary>
    public void Invalidate(string catalog)
    {
        if (tokens.TryRemove(catalog, out var token))
        {
            token.Cancel();
            token.Dispose();
        }
    }
}
