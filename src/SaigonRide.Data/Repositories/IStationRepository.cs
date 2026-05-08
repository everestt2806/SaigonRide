using SaigonRide.Domain.Entities;

namespace SaigonRide.Data.Repositories;

public interface IStationRepository : IRepository<Station>
{
}

public class StationRepository : Repository<Station>, IStationRepository
{
    public StationRepository(SaigonRideDbContext db) : base(db) { }
}
