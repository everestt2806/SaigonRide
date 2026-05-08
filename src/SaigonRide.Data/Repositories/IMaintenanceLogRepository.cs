using SaigonRide.Domain.Entities;

namespace SaigonRide.Data.Repositories;

public interface IMaintenanceLogRepository : IRepository<MaintenanceLog>
{
}

public class MaintenanceLogRepository : Repository<MaintenanceLog>, IMaintenanceLogRepository
{
    public MaintenanceLogRepository(SaigonRideDbContext db) : base(db) { }
}
