namespace TIAdmin.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Infrastructure.Persistence;

public sealed class UserDirectory(TIAdminDbContext context) : IUserDirectory
{
    public async Task<UserReference?> FindAsync(int userId, CancellationToken cancellationToken = default) =>
        await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserReference(u.Id, u.UserName!, (u.FirstName + " " + u.LastName).Trim(), u.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
}
