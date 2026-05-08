using System.Linq.Expressions;

namespace SaigonRide.Data.Repositories;

/// <summary>
/// Generic repository contract (CRC card / SOLID §7.2.1 Interface Segregation).
/// Concrete implementations forward to <see cref="SaigonRideDbContext"/>; tests
/// can swap in an in-memory variant.
/// </summary>
public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<TEntity>> ListAsync(CancellationToken ct = default);
    IQueryable<TEntity> Query(bool tracking = true, bool ignoreFilters = false);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
}
