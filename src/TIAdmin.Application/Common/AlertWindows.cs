namespace TIAdmin.Application.Common;

/// <summary>
/// Ventanas de alerta configurables (p. ej. "90,60,30,15,7" dias antes del vencimiento).
/// </summary>
public static class AlertWindows
{
    /// <summary>
    /// Umbral mas pequeno que aun cubre <paramref name="daysRemaining"/>: con 90/60/30/15/7 y 20 dias
    /// restantes devuelve 30. Null si esta fuera de toda ventana o ya vencio.
    /// </summary>
    public static int? Resolve(int daysRemaining, IReadOnlyCollection<int> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        if (daysRemaining < 0)
        {
            return null;
        }

        var covering = windows.Where(w => w >= daysRemaining).ToList();
        return covering.Count == 0 ? null : covering.Min();
    }

    public static int Max(IReadOnlyCollection<int> windows) => windows.Count == 0 ? 0 : windows.Max();
}
