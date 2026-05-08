using Microsoft.EntityFrameworkCore;
using SaigonRide.Domain.Entities;
using SaigonRide.Domain.Enums;

namespace SaigonRide.Data.Repositories;

public interface IRentalRepository : IRepository<Rental>
{
    Task<Rental?> GetActiveForUserAsync(int userId, CancellationToken ct = default);
    Task<Rental?> GetByIdWithRelationsAsync(int id, CancellationToken ct = default);
    Task<Rental?> GetByIdForUserAsync(int rentalId, int userId, CancellationToken ct = default);
    Task<List<Rental>> ListByUserAsync(int userId, CancellationToken ct = default);
}

public class RentalRepository : Repository<Rental>, IRentalRepository
{
    public RentalRepository(SaigonRideDbContext db) : base(db) { }

    public Task<Rental?> GetActiveForUserAsync(int userId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(r => r.UserId == userId && r.Status == RentalStatus.Active, ct);

    public Task<Rental?> GetByIdWithRelationsAsync(int id, CancellationToken ct = default) =>
        Set.Include(r => r.Vehicle!).ThenInclude(v => v.Category!)
           .Include(r => r.PickupStation!)
           .Include(r => r.ReturnStation!)
           .Include(r => r.User!)
           .Include(r => r.Transaction!)
           .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Rental?> GetByIdForUserAsync(int rentalId, int userId, CancellationToken ct = default) =>
        Set.Include(r => r.Vehicle!).ThenInclude(v => v.Category!)
           .Include(r => r.PickupStation!)
           .Include(r => r.ReturnStation!)
           .Include(r => r.Transaction!)
           .FirstOrDefaultAsync(r => r.Id == rentalId && r.UserId == userId, ct);

    public Task<List<Rental>> ListByUserAsync(int userId, CancellationToken ct = default) =>
        Set.AsNoTracking()
           .Include(r => r.Vehicle!).ThenInclude(v => v.Category!)
           .Include(r => r.PickupStation!)
           .Include(r => r.ReturnStation!)
           .Include(r => r.Transaction!)
           .Where(r => r.UserId == userId)
           .OrderByDescending(r => r.StartTime)
           .ToListAsync(ct);
}
