namespace TIAdmin.Infrastructure.Services;

using TIAdmin.Application.Common.Interfaces;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime Today => DateTime.UtcNow.Date;
}
