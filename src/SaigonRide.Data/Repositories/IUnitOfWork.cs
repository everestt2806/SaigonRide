using Microsoft.EntityFrameworkCore.Storage;

namespace SaigonRide.Data.Repositories;

/// <summary>
/// Wraps <c>DbContext.SaveChanges</c> and exposes an explicit transaction so
/// service-layer orchestrators (e.g., <c>RentalService.EndRental</c>) can
/// commit several writes atomically (UC-02 main scenario step 12, §7.1.3).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly SaigonRideDbContext _db;
    public UnitOfWork(SaigonRideDbContext db) => _db = db;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        _db.Database.BeginTransactionAsync(ct);
}
