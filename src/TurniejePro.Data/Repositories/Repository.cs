using Microsoft.EntityFrameworkCore;
using TurniejePro.Domain.Repositories;

namespace TurniejePro.Data.Repositories;

/// <summary>
/// Generyczna implementacja repozytorium oparta na Entity Framework Core.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected TurniejeProDbContext Context { get; }
    protected DbSet<T> DbSet { get; }

    public Repository(TurniejeProDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync([id], cancellationToken);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
    }

    public virtual void Update(T entity)
    {
        DbSet.Update(entity);
    }

    public virtual void Delete(T entity)
    {
        DbSet.Remove(entity);
    }
}
