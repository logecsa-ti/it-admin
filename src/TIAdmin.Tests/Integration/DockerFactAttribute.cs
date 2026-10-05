namespace TIAdmin.Tests.Integration;

using System.Runtime.InteropServices;

/// <summary>
/// Prueba que necesita Docker: se omite (no falla) si no hay motor disponible o si
/// TIADMIN_SKIP_DOCKER_TESTS=true. En CI (ubuntu-latest) Docker esta disponible y se ejecutan.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerAvailability.IsAvailable)
        {
            Skip = "Docker no disponible (o TIADMIN_SKIP_DOCKER_TESTS=true).";
        }
    }
}

public static class DockerAvailability
{
    private static readonly Lazy<bool> Available = new(Detect);

    public static bool IsAvailable => Available.Value;

    private static bool Detect()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("TIADMIN_SKIP_DOCKER_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Environment.GetEnvironmentVariable("DOCKER_HOST") is { Length: > 0 })
        {
            return true;
        }

        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? File.Exists(@"\\.\pipe\docker_engine")
            : File.Exists("/var/run/docker.sock");
    }
}
