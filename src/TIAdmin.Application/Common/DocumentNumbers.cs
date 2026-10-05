namespace TIAdmin.Application.Common;

using TIAdmin.Application.Common.Interfaces;

/// <summary>
/// Numeracion de documentos (ADR-028): PREFIJO-AAAA-{Id:000000}. Se asigna despues de insertar,
/// con un numero provisional unico mientras tanto. El prefijo se configura en SystemConfigurations.
/// </summary>
public static class DocumentNumbers
{
    public const int MaxPrefixLength = 6;

    /// <summary>Valor provisional unico de 20 caracteres hasta conocer el Id.</summary>
    public static string Provisional() => $"TMP-{Guid.NewGuid():N}"[..20];

    public static async Task<string> FormatAsync(
        ISystemSettings settings,
        IClock clock,
        string prefixKey,
        string fallbackPrefix,
        int id,
        DateTime createdUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);

        var prefix = await settings.GetStringAsync(prefixKey, fallbackPrefix, cancellationToken);
        prefix = prefix.Length > MaxPrefixLength ? prefix[..MaxPrefixLength] : prefix;
        var year = TimeZoneInfo.ConvertTimeFromUtc(createdUtc, clock.TimeZone).Year;
        return $"{prefix}-{year}-{id:D6}";
    }
}
