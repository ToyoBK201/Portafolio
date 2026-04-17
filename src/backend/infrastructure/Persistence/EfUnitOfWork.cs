using SolicitudesTechGov.Application.Abstractions;

namespace SolicitudesTechGov.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly SolicitudesTechGovDbContext _dbContext;

    public EfUnitOfWork(SolicitudesTechGovDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
