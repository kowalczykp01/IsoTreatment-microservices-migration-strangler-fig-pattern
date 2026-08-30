using Microsoft.EntityFrameworkCore;
using TreatmentService.Application.Abstractions;
using TreatmentService.Infrastructure.Persistence;

namespace TreatmentService.Infrastructure.Identity;

internal sealed class SharedDatabaseUserDirectory : IUserDirectory
{
    private readonly TreatmentDbContext _dbContext;

    public SharedDatabaseUserDirectory(TreatmentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(int userId, CancellationToken cancellationToken = default) =>
        _dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken);
}
