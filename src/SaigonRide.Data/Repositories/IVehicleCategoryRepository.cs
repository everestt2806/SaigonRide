using SaigonRide.Domain.Entities;

namespace SaigonRide.Data.Repositories;

public interface IVehicleCategoryRepository : IRepository<VehicleCategory>
{
}

public class VehicleCategoryRepository : Repository<VehicleCategory>, IVehicleCategoryRepository
{
    public VehicleCategoryRepository(SaigonRideDbContext db) : base(db) { }
}
