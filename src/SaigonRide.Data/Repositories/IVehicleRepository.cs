using Microsoft.EntityFrameworkCore;
using SaigonRide.Domain.Entities;

namespace SaigonRide.Data.Repositories;

public interface IVehicleRepository : IRepository<Vehicle>
{
    Task<bool> ExistsByPlateAsync(string licensePlate, int? excludeId = null, CancellationToken ct = default);
    Task<Vehicle?> GetByIdWithRelationsAsync(int id, CancellationToken ct = default);
}

public class VehicleRepository : Repository<Vehicle>, IVehicleRepository
{
    public VehicleRepository(SaigonRideDbContext db) : base(db) { }

    public async Task<bool> ExistsByPlateAsync(string licensePlate, int? excludeId = null, CancellationToken ct = default)
    {
        var query = Set.IgnoreQueryFilters().Where(v => v.LicensePlate == licensePlate);
        if (excludeId.HasValue)
            query = query.Where(v => v.Id != excludeId.Value);
        return await query.AnyAsync(ct);
    }

    public Task<Vehicle?> GetByIdWithRelationsAsync(int id, CancellationToken ct = default) =>
        Set.Include(v => v.Category!)
           .Include(v => v.HomeStation!)
           .FirstOrDefaultAsync(v => v.Id == id, ct);
}
