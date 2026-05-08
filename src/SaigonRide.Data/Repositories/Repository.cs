using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace SaigonRide.Data.Repositories;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly SaigonRideDbContext Db;
    protected readonly DbSet<TEntity> Set;

    public Repository(SaigonRideDbContext db)
    {
        Db = db;
        Set = db.Set<TEntity>();
    }

    public Task<TEntity?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Set.FindAsync(new object?[] { id }, ct).AsTask();

    public Task<List<TEntity>> ListAsync(CancellationToken ct = default) =>
        Set.AsNoTracking().ToListAsync(ct);

    public IQueryable<TEntity> Query(bool tracking = true, bool ignoreFilters = false)
    {
        IQueryable<TEntity> q = Set;
        if (!tracking) q = q.AsNoTracking();
        if (ignoreFilters) q = q.IgnoreQueryFilters();
        return q;
    }

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await Set.AddAsync(entity, ct);
        return entity;
    }

    public void Update(TEntity entity) => Set.Update(entity);

    public void Remove(TEntity entity) => Set.Remove(entity);

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default) =>
        Set.AnyAsync(predicate, ct);
}
